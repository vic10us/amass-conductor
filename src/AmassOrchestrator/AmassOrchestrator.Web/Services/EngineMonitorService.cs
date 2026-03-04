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

    public EngineMonitorService(
        IKubernetesDiscoveryService discovery,
        IAmassEngineClient engineClient,
        EngineStateStore stateStore,
        IOptionsMonitor<OrchestratorOptions> options,
        ILogger<EngineMonitorService> logger)
    {
        _discovery = discovery;
        _engineClient = engineClient;
        _stateStore = stateStore;
        _options = options;
        _logger = logger;
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
                foreach (var sessionToken in listResponse.SessionTokens)
                {
                    var stats = await _engineClient.GetSessionStatsAsync(pod.PodIP, port, sessionToken, token);
                    sessions.Add(new SessionInfo(
                        sessionToken,
                        stats?.WorkItemsCompleted ?? 0,
                        stats?.WorkItemsTotal ?? 0));
                }
            }

            var state = new EngineInstanceState(pod, isHealthy, sessions);
            _stateStore.UpdateState(pod.PodName, state);
        });

        _stateStore.RemoveStale(activePodNames);
    }
}
