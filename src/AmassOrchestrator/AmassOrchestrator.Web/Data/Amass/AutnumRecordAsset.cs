namespace AmassOrchestrator.Web.Data.Amass;

public class AutnumRecordAsset
{
    public long Id { get; set; }
    public string Handle { get; set; } = string.Empty;
    public int Asn { get; set; }
    public string? RecordName { get; set; }
    public string? WhoisServer { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
