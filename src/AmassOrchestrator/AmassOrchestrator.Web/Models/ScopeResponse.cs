using System.Text.Json;
using System.Text.Json.Serialization;

namespace AmassOrchestrator.Web.Models;

public class ScopeResponse
{
    [JsonPropertyName("data")]
    public List<JsonElement> Data { get; set; } = [];
}
