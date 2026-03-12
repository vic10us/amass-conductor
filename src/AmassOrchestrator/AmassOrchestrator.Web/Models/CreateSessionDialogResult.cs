namespace AmassOrchestrator.Web.Models;

public class CreateSessionDialogResult
{
    public AmassConfig Config { get; set; } = new();
    public SeedAssets Assets { get; set; } = new();
    public bool SubmitDomainsAsFqdnAssets { get; set; } = true;
    public bool UseSameEngine { get; set; }
    public string? TargetEnginePodName { get; set; }
}
