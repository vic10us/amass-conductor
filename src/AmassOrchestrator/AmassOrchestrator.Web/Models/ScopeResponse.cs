using System.Text.Json.Serialization;

namespace AmassOrchestrator.Web.Models;

public class ScopeResponse
{
    [JsonPropertyName("data")]
    public List<byte[]> Data { get; set; } = [];
}
