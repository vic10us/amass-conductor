using System.Net;

namespace AmassOrchestrator.Web.Data.Amass;

public class IpAddressAsset
{
    public long Id { get; set; }
    public IPAddress IpAddress { get; set; } = IPAddress.None;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
