using k8s;
using k8s.KubeConfigModels;

namespace AmassOrchestrator.Web.Services;

public class KubernetesContextService
{
    /// <summary>
    /// Returns all context names found in the kubeconfig file, or an empty list if
    /// the file cannot be read (e.g. when running in-cluster with no local kubeconfig).
    /// </summary>
    public IReadOnlyList<string> GetAvailableContexts(string? kubeConfigPath = null)
    {
        try
        {
            var k8sConfig = KubernetesClientConfiguration.LoadKubeConfig(kubeConfigPath);
            return k8sConfig.Contexts?.Select(c => c.Name).Where(n => !string.IsNullOrEmpty(n)).ToList()
                ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Returns the current-context name from the kubeconfig file, or null if unavailable.
    /// </summary>
    public string? GetCurrentContext(string? kubeConfigPath = null)
    {
        try
        {
            var k8sConfig = KubernetesClientConfiguration.LoadKubeConfig(kubeConfigPath);
            return k8sConfig.CurrentContext;
        }
        catch
        {
            return null;
        }
    }
}
