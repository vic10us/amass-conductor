using System.Net;

namespace AmassOrchestrator.Web.Data.Amass;

public class NetblockAsset
{
    public long Id { get; set; }
    public IPNetwork NetblockCidr { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
