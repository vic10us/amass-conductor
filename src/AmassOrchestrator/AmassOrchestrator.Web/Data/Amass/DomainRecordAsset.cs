namespace AmassOrchestrator.Web.Data.Amass;

public class DomainRecordAsset
{
    public long Id { get; set; }
    public string Domain { get; set; } = string.Empty;
    public string? RecordName { get; set; }
    public string? Punycode { get; set; }
    public string? Extension { get; set; }
    public string? WhoisServer { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
