namespace AmassOrchestrator.Web.Data;

public class SessionRecord
{
    public int Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public string EnginePodName { get; set; } = string.Empty;
    public int WorkItemsCompleted { get; set; }
    public int WorkItemsTotal { get; set; }
    public bool IsCompleted { get; set; }
    public int ConsecutiveCompletionPolls { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? Domains { get; set; }
    public string? ConfigJson { get; set; }
    public bool IsFailed { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsCancelled { get; set; }
}
