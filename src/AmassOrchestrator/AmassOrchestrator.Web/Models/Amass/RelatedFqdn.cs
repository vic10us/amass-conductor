namespace AmassOrchestrator.Web.Models.Amass;

public class RelatedFqdn
{
    public long Id { get; set; }
    public string Fqdn { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string EdgeLabel { get; set; } = string.Empty;
}
