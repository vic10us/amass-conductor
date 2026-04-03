namespace AmassOrchestrator.Web.Data;

public class ConductorHeartbeat
{
    public string InstanceId { get; set; } = string.Empty;
    public DateTime LastHeartbeatUtc { get; set; }
}
