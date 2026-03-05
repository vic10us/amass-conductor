namespace AmassOrchestrator.Web.Models;

public class TransformationRow
{
    public bool Enabled { get; set; }
    public string Key { get; set; } = string.Empty;
    public int Priority { get; set; }
    public int Confidence { get; set; }
    public int Ttl { get; set; }
}
