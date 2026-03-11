using AmassOrchestrator.Web.Configuration;
using AmassOrchestrator.Web.Models.Kubernetes;
using k8s;
using Microsoft.Extensions.Options;

namespace AmassOrchestrator.Web.Services;

public class KubernetesDiscoveryService : IKubernetesDiscoveryService
{
    private readonly IKubernetes _client;
    private readonly OrchestratorOptions _options;
    private readonly ILogger<KubernetesDiscoveryService> _logger;

    public KubernetesDiscoveryService(
        IKubernetes client,
        IOptions<OrchestratorOptions> options,
        ILogger<KubernetesDiscoveryService> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<List<EnginePodInfo>> DiscoverEnginePodsAsync(CancellationToken cancellationToken = default)
    {
        var labelSelector = _options.LabelSelector;

        try
        {
            var podList = await _client.CoreV1.ListNamespacedPodAsync(
                _options.Namespace,
                labelSelector: labelSelector,
                cancellationToken: cancellationToken);

            return podList.Items
                .Where(p => !string.IsNullOrEmpty(p.Status?.PodIP))
                .Select(MapPod)
                .OrderBy(p => p.Ordinal)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover engine pods in namespace {Namespace}", _options.Namespace);
            return [];
        }
    }

    private static EnginePodInfo MapPod(k8s.Models.V1Pod pod)
    {
        var name = pod.Metadata.Name;
        var ip = pod.Status.PodIP;
        var phase = pod.Status.Phase ?? "Unknown";

        var ordinal = 0;
        var lastDash = name.LastIndexOf('-');
        if (lastDash >= 0 && int.TryParse(name[(lastDash + 1)..], out var parsed))
            ordinal = parsed;

        var isReady = pod.Status.Conditions?
            .Any(c => c.Type == "Ready" && c.Status == "True") ?? false;

        var annotations = pod.Metadata.Annotations;

        return new EnginePodInfo(name, ip, ordinal, phase, isReady, annotations);
    }
}
