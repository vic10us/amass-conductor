namespace AmassOrchestrator.Web.Models.Kubernetes;

public record SessionInfo(
    string Token,
    int WorkItemsCompleted,
    int WorkItemsTotal)
{
    public double ProgressPercent =>
        WorkItemsTotal > 0
            ? Math.Round(100.0 * WorkItemsCompleted / WorkItemsTotal, 1)
            : 0;
}
