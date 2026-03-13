namespace AmassOrchestrator.Web.Models.Kubernetes;

public record EnginePodInfo(
    string PodName,
    string PodIP,
    int Ordinal,
    string Phase,
    bool IsReady,
    IDictionary<string, string>? Annotations = null)
{
    public PodPhase PodPhase()
    {
        return Phase switch
        {
            "Pending" => Kubernetes.PodPhase.Pending,
            "Running" => Kubernetes.PodPhase.Running,
            "Succeeded" => Kubernetes.PodPhase.Succeeded,
            "Failed" => Kubernetes.PodPhase.Failed,
            _ => Kubernetes.PodPhase.Unknown
        };
    }
};

public enum PodPhase { Pending, Running, Succeeded, Failed, Unknown }