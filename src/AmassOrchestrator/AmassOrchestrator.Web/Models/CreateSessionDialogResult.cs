namespace AmassOrchestrator.Web.Models;

public class CreateSessionDialogResult
{
    public AmassConfig Config { get; set; } = new();
    public bool UseSameEngine { get; set; }
    public string? TargetEnginePodName { get; set; }
}
