namespace AmassOrchestrator.Web.Data.Amass;

public class AmassEdge
{
    public long EdgeId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public short EtypeId { get; set; }
    public string Label { get; set; } = string.Empty;
    public long FromEntityId { get; set; }
    public long ToEntityId { get; set; }
}
