namespace AmassOrchestrator.Web.Data.Amass;

public class ServiceAsset
{
    public long Id { get; set; }
    public string UniqueId { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
