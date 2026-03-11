namespace AmassOrchestrator.Web.Models.Kubernetes;

public record SessionInfo(
    string Token,
    int WorkItemsCompleted,
    int WorkItemsTotal,
    bool IsCompleted = false,
    int ConsecutiveCompletionPolls = 0,
    double ItemsPerSecond = 0,
    DateTime? LastPollTimestamp = null)
{
    public double ProgressPercent =>
        WorkItemsTotal > 0
            ? Math.Round(100.0 * WorkItemsCompleted / WorkItemsTotal, 1)
            : 0;
}
