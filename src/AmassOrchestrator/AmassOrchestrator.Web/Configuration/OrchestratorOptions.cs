namespace AmassOrchestrator.Web.Configuration;

public class OrchestratorOptions
{
    public const string SectionName = "Orchestrator";

    public string Namespace { get; set; } = "amass";
    public string StatefulSetName { get; set; } = "amass-engine";
    public int EnginePort { get; set; } = 4000;
    public int PollIntervalSeconds { get; set; } = 2;
    public string LabelSelector { get; set; } = "app.kubernetes.io/name=amass,app.kubernetes.io/component=engine";
    public string EngineBasePath { get; set; } = "/api/v1";
    public int HttpClientTimeoutSeconds { get; set; } = 10;
    public int WebSocketBufferSize { get; set; } = 4096;
    public int CompletionPollThreshold { get; set; } = 5;
    public string[] SupportedDatabaseSystems { get; set; } = ["postgres", "mysql"];
    public string BruteForceWordlistFile { get; set; } = "wordlists/default-namelist.txt";
    public string AlterationsWordlistFile { get; set; } = "wordlists/default-alterations.txt";
    public string DefaultTransformationsFile { get; set; } = "wordlists/default-transformations.json";
    public string DatabasePath { get; set; } = "App_Data/orchestrator.db";
    public int MaxActiveSessionsPerEngine { get; set; } = 1;
    public bool AutoDeleteCompletedSessions { get; set; } = true;
    public string InstanceId { get; set; } = Environment.MachineName;
    public string? AmassDbConnectionString { get; set; }
    public string? OrchestratorDbConnectionString { get; set; }
    public int BulkAssetBatchSize { get; set; } = 1000;

    // Out-of-cluster kubeconfig settings (ignored when running in-cluster)
    public string? KubeConfigPath { get; set; }
    public string? KubeContext { get; set; }
}
