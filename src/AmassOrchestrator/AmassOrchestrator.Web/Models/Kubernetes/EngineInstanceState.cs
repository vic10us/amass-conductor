namespace AmassOrchestrator.Web.Models.Kubernetes;

public record EngineInstanceState(
    EnginePodInfo Pod,
    bool IsHealthy,
    List<SessionInfo> Sessions,
    TorCheckResult? TorCheck = null);
