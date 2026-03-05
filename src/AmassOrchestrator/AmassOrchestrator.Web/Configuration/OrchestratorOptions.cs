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
    public int HttpClientTimeoutSeconds { get; set; } = 10;
    public int WebSocketBufferSize { get; set; } = 4096;
    public int CompletionPollThreshold { get; set; } = 5;
    public string[] SupportedDatabaseSystems { get; set; } = ["postgres", "mysql"];
    public string LogFilePath { get; set; } = "logs/orchestrator-.log";
    public int LogRetainedFileCount { get; set; } = 31;
    public string BruteForceWordlistFile { get; set; } = "wordlists/default-namelist.txt";
    public string AlterationsWordlistFile { get; set; } = "wordlists/default-alterations.txt";
    public string DefaultTransformationsFile { get; set; } = "wordlists/default-transformations.json";
}
