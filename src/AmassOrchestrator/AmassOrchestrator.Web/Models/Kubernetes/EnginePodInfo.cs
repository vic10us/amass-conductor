namespace AmassOrchestrator.Web.Models.Kubernetes;

public record EnginePodInfo(
    string PodName,
    string PodIP,
    int Ordinal,
    string Phase,
    bool IsReady,
    IDictionary<string, string>? Annotations = null);
