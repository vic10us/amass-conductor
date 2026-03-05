namespace AmassOrchestrator.Web.Data;

public class LogRecord
{
    public long Id { get; set; }
    public string SessionToken { get; set; } = string.Empty;
    public string EnginePodName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }
}
