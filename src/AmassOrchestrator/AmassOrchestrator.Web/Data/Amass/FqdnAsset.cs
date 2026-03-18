namespace AmassOrchestrator.Web.Data.Amass;

public class FqdnAsset
{
    public long Id { get; set; }
    public string Fqdn { get; set; } = string.Empty;
    public string? ReverseFqdn { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
