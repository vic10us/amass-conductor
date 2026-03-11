using AmassOrchestrator.Web.Configuration;
using AmassOrchestrator.Web.Models.Kubernetes;
using Microsoft.Extensions.Options;

namespace AmassOrchestrator.Web.Services;

public class EngineMonitorService : BackgroundService
{
    private readonly IKubernetesDiscoveryService _discovery;
    private readonly IAmassEngineClient _engineClient;
    private readonly EngineStateStore _stateStore;
    private readonly IOptionsMonitor<OrchestratorOptions> _options;
    private readonly ILogger<EngineMonitorService> _logger;
    private readonly ISessionRepository _sessionRepository;

    public EngineMonitorService(
        IKubernetesDiscoveryService discovery,
        IAmassEngineClient engineClient,
        EngineStateStore stateStore,
        IOptionsMonitor<OrchestratorOptions> options,
        ILogger<EngineMonitorService> logger,
        ISessionRepository sessionRepository)
    {
        _discovery = discovery;
        _engineClient = engineClient;
        _stateStore = stateStore;
        _options = options;
        _logger = logger;
        _sessionRepository = sessionRepository;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Engine monitor service starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during engine poll cycle");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.CurrentValue.PollIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    internal async Task PollAsync(CancellationToken ct)
    {
        var pods = await _discovery.DiscoverEnginePodsAsync(ct);
        var port = _options.CurrentValue.EnginePort;
        var activePodNames = new HashSet<string>(pods.Select(p => p.PodName));

        await Parallel.ForEachAsync(pods, ct, async (pod, token) =>
        {
            var health = await _engineClient.HealthCheckAsync(pod.PodIP, port, token);
            var isHealthy = health?.Result == "Amass Engine OK";

            var sessions = new List<SessionInfo>();
            var listResponse = await _engineClient.ListSessionsAsync(pod.PodIP, port, token);

            if (listResponse?.SessionTokens != null)
            {
                var previousState = _stateStore.GetState(pod.PodName);

                foreach (var sessionToken in listResponse.SessionTokens)
                {
                    var stats = await _engineClient.GetSessionStatsAsync(pod.PodIP, port, sessionToken, token);
                    var completed = stats?.WorkItemsCompleted ?? 0;
                    var total = stats?.WorkItemsTotal ?? 0;

                    var previous = previousState?.Sessions.FirstOrDefault(s => s.Token == sessionToken);
                    var consecutivePolls = previous?.ConsecutiveCompletionPolls ?? 0;
                    var isCompleted = previous?.IsCompleted ?? false;

                    var now = DateTime.UtcNow;
                    double itemsPerSecond = 0;
                    if (previous?.LastPollTimestamp != null)
                    {
                        var elapsed = (now - previous.LastPollTimestamp.Value).TotalSeconds;
                        var delta = completed - previous.WorkItemsCompleted;
                        if (elapsed > 0 && delta > 0)
                            itemsPerSecond = Math.Round(delta / elapsed, 2);
                    }

                    if (!isCompleted)
                    {
                        if (total > 0 && completed == total)
                        {
                            consecutivePolls++;
                        }
                        else
                        {
                            consecutivePolls = 0;
                        }

                        if (consecutivePolls >= _options.CurrentValue.CompletionPollThreshold)
                        {
                            isCompleted = true;

                            if (_options.CurrentValue.AutoDeleteCompletedSessions)
                            {
                                try
                                {
                                    var deleted = await _engineClient.DeleteSessionAsync(pod.PodIP, port, sessionToken, token);
                                    if (deleted)
                                        _logger.LogInformation("Auto-deleted completed session {Token} from engine {Pod}", sessionToken, pod.PodName);
                                    else
                                        _logger.LogWarning("Auto-delete of session {Token} from engine {Pod} returned false", sessionToken, pod.PodName);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Failed to auto-delete completed session {Token} from engine {Pod}", sessionToken, pod.PodName);
                                }
                            }
                        }
                    }

                    var sessionInfo = new SessionInfo(
                        sessionToken,
                        completed,
                        total,
                        isCompleted,
                        consecutivePolls,
                        itemsPerSecond,
                        now);

                    sessions.Add(sessionInfo);

                    try
                    {
                        await _sessionRepository.UpsertAsync(pod.PodName, sessionInfo);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to persist session {Token} to database", sessionToken);
                    }
                }
            }

            var state = new EngineInstanceState(pod, isHealthy, sessions);
            _stateStore.UpdateState(pod.PodName, state);
        });

        _stateStore.RemoveStale(activePodNames);
    }
}
