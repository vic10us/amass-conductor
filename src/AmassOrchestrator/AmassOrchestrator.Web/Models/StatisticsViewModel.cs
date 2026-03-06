namespace AmassOrchestrator.Web.Models;

public class StatisticsViewModel
{
    public int TotalSessions { get; set; }
    public int CompletedSessions { get; set; }
    public int FailedSessions { get; set; }
    public int ActiveSessions { get; set; }
    public int TotalWorkItemsProcessed { get; set; }
    public double SuccessRate { get; set; }
    public double AverageDurationMinutes { get; set; }
    public double MinDurationMinutes { get; set; }
    public double MaxDurationMinutes { get; set; }

    public List<SessionsOverTimeEntry> SessionsOverTime { get; set; } = [];
    public List<DurationPerSessionEntry> DurationPerSession { get; set; } = [];
    public List<EngineDistributionEntry> EngineDistribution { get; set; } = [];
    public List<WorkItemsPerSessionEntry> WorkItemsPerSession { get; set; } = [];
    public List<StatusDistributionEntry> StatusDistribution { get; set; } = [];
}

public class SessionsOverTimeEntry
{
    public string Date { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class DurationPerSessionEntry
{
    public string Token { get; set; } = string.Empty;
    public double DurationMinutes { get; set; }
}

public class EngineDistributionEntry
{
    public string EnginePodName { get; set; } = string.Empty;
    public int SessionCount { get; set; }
}

public class WorkItemsPerSessionEntry
{
    public string Token { get; set; } = string.Empty;
    public int WorkItems { get; set; }
}

public class StatusDistributionEntry
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
}
