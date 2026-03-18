namespace AmassOrchestrator.Web.Models.Amass;

public class RelatedIpAddress
{
    public long Id { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string EdgeLabel { get; set; } = string.Empty;
}
