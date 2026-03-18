namespace AmassOrchestrator.Web.Data.Amass;

public class TlsCertificateAsset
{
    public long Id { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string SubjectCommonName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
