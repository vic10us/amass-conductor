using AmassOrchestrator.Web.Models.Kubernetes;

namespace AmassOrchestrator.Web.Services;

public interface IKubernetesDiscoveryService
{
    Task<List<EnginePodInfo>> DiscoverEnginePodsAsync(CancellationToken cancellationToken = default);
}
