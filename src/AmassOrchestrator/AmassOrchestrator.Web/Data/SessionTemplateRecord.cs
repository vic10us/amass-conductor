namespace AmassOrchestrator.Web.Data;

public class SessionTemplateRecord
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string InstanceId { get; set; } = string.Empty;
    public string ConfigJson { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
