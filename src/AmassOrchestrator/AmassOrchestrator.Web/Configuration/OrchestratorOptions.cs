namespace AmassOrchestrator.Web.Configuration;

public class OrchestratorOptions
{
    public const string SectionName = "Orchestrator";

    public string Namespace { get; set; } = "amass";
    public string StatefulSetName { get; set; } = "amass-engine";
    public int EnginePort { get; set; } = 4000;
    public int PollIntervalSeconds { get; set; } = 10;
    public string LabelSelector { get; set; } = "app.kubernetes.io/name=amass,app.kubernetes.io/component=engine";
    public string EngineBasePath { get; set; } = "/api/v1";
}
