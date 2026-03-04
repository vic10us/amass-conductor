using System.Text.Json.Serialization;

namespace AmassOrchestrator.Web.Models;

public class HealthCheckResponse
{
    [JsonPropertyName("result")]
    public string Result { get; set; } = string.Empty;
}
