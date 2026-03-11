namespace AmassOrchestrator.Web.Models;

public class SessionViewModel
{
    public string Token { get; set; } = string.Empty;
    public string EnginePodName { get; set; } = string.Empty;
    public int WorkItemsCompleted { get; set; }
    public int WorkItemsTotal { get; set; }
    public double ProgressPercent => WorkItemsTotal > 0 ? Math.Round(100.0 * WorkItemsCompleted / WorkItemsTotal, 1) : 0;
    public bool IsCompleted { get; set; }
    public bool IsLive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? Domains { get; set; }
    public string? PodIp { get; set; }
    public bool IsFailed { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsCancelled { get; set; }
}
