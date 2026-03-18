namespace AmassOrchestrator.Web.Models.Amass;

public class RelatedNetblock
{
    public long Id { get; set; }
    public string Cidr { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string EdgeLabel { get; set; } = string.Empty;
}
