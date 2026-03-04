namespace AmassOrchestrator.Web.Configuration;

public class OrchestratorOptions
{
    public const string SectionName = "Orchestrator";

    public string Namespace { get; set; } = "default";
    public string StatefulSetName { get; set; } = "amass-engine";
    public int EnginePort { get; set; } = 8080;
    public int PollIntervalSeconds { get; set; } = 10;
    public string LabelSelector { get; set; } = "app.kubernetes.io/name=amass,app.kubernetes.io/component=engine";
    public string EngineBasePath { get; set; } = "/api/v1";
}
