namespace AmassOrchestrator.Web.Models;

public class SeedAssets
{
    public List<string> Fqdns { get; set; } = [];
    public List<string> IpAddresses { get; set; } = [];
    public List<int> AutonomousSystems { get; set; } = [];
    public List<string> Netblocks { get; set; } = [];
    public List<string> Organizations { get; set; } = [];
    public List<string> Locations { get; set; } = [];

    public bool HasAny() =>
        Fqdns.Count > 0 || IpAddresses.Count > 0 || AutonomousSystems.Count > 0 ||
        Netblocks.Count > 0 || Organizations.Count > 0 || Locations.Count > 0;
}
