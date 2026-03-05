namespace AmassOrchestrator.Web.Models;

public record AggregatedLogEntry(
    DateTime TimestampUtc,
    string EnginePodName,
    string SessionToken,
    string Message);
