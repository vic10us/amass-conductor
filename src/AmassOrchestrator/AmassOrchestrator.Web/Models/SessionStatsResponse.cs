using System.Text.Json.Serialization;

namespace AmassOrchestrator.Web.Models;

public class SessionStatsResponse
{
    [JsonPropertyName("workItemsCompleted")]
    public int WorkItemsCompleted { get; set; }

    [JsonPropertyName("workItemsTotal")]
    public int WorkItemsTotal { get; set; }
}
