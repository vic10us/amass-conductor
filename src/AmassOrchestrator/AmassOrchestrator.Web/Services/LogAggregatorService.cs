using System.Collections.Concurrent;
using AmassOrchestrator.Web.Configuration;
using AmassOrchestrator.Web.Models;
using AmassOrchestrator.Web.Models.Kubernetes;
using Microsoft.Extensions.Options;

namespace AmassOrchestrator.Web.Services;

public class LogAggregatorService : BackgroundService
{
    private readonly EngineStateStore _stateStore;
    private readonly IAmassEngineClient _engineClient;
    private readonly ISessionRepository _sessionRepository;
    private readonly IOptions<OrchestratorOptions> _options;
    private readonly ILogger<LogAggregatorService> _logger;

    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeStreams = new();
    private readonly TaskCompletionSource _stateChangedSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile TaskCompletionSource _changeSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public event Action<AggregatedLogEntry>? OnLogReceived;

    public LogAggregatorService(
        EngineStateStore stateStore,
        IAmassEngineClient engineClient,
        ISessionRepository sessionRepository,
        IOptions<OrchestratorOptions> options,
        ILogger<LogAggregatorService> logger)
    {
        _stateStore = stateStore;
        _engineClient = engineClient;
        _sessionRepository = sessionRepository;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Log aggregator service starting");

        _stateStore.OnStateChanged += SignalStateChanged;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await SyncStreamsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error syncing log streams");
                }

                // Wait for next state change or timeout
                var signal = _changeSignal;
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                cts.CancelAfter(TimeSpan.FromSeconds(30));

                try
                {
                    await signal.Task.WaitAsync(cts.Token);
                }
                catch (OperationCanceledException) { }

                // Reset signal
                Interlocked.Exchange(ref _changeSignal, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
            }
        }
        finally
        {
            _stateStore.OnStateChanged -= SignalStateChanged;

            // Cancel all active streams
            foreach (var kvp in _activeStreams)
            {
                kvp.Value.Cancel();
                kvp.Value.Dispose();
            }
            _activeStreams.Clear();
        }
    }

    private void SignalStateChanged()
    {
        _changeSignal.TrySetResult();
    }

    private async Task SyncStreamsAsync()
    {
        // Only stream logs for sessions owned by this instance
        var ownedTokens = await _sessionRepository.GetOwnedActiveTokensAsync();

        // Collect all active (non-completed) sessions with their pod info
        var activeSessions = new Dictionary<string, (string PodIp, string PodName)>();

        foreach (var kvp in _stateStore.States)
        {
            var podName = kvp.Key;
            var state = kvp.Value;
            if (!state.IsHealthy || !state.Pod.IsReady) continue;

            foreach (var session in state.Sessions)
            {
                if (!session.IsCompleted && ownedTokens.Contains(session.Token))
                {
                    activeSessions[session.Token] = (state.Pod.PodIP, podName);
                }
            }
        }

        // Start streams for new sessions
        foreach (var (token, (podIp, podName)) in activeSessions)
        {
            if (!_activeStreams.ContainsKey(token))
            {
                var cts = new CancellationTokenSource();
                if (_activeStreams.TryAdd(token, cts))
                {
                    _ = StreamSessionLogs(token, podIp, podName, cts.Token);
                }
            }
        }

        // Cancel streams for sessions that are no longer active
        foreach (var token in _activeStreams.Keys)
        {
            if (!activeSessions.ContainsKey(token))
            {
                if (_activeStreams.TryRemove(token, out var cts))
                {
                    cts.Cancel();
                    cts.Dispose();
                    _logger.LogDebug("Stopped log stream for completed/removed session {Token}", token);
                }
            }
        }
    }

    private async Task StreamSessionLogs(string sessionToken, string podIp, string podName, CancellationToken ct)
    {
        var port = _options.Value.EnginePort;
        _logger.LogDebug("Starting log stream for session {Token} on {PodName}", sessionToken, podName);

        try
        {
            await foreach (var line in _engineClient.StreamLogsAsync(podIp, port, sessionToken, ct))
            {
                var entry = new AggregatedLogEntry(DateTime.UtcNow, podName, sessionToken, line);
                OnLogReceived?.Invoke(entry);

                try
                {
                    await _sessionRepository.AddLogAsync(sessionToken, podName, line);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to persist log for session {Token}", sessionToken);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Log stream ended for session {Token} on {PodName}", sessionToken, podName);
        }
        finally
        {
            _activeStreams.TryRemove(sessionToken, out _);
        }
    }
}
