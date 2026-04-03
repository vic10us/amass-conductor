namespace AmassOrchestrator.Web.Models;

public class SaveAsTemplateDialogResult
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ConfigJson { get; set; } = string.Empty;
}
