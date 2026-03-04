using System.Text.Json.Serialization;

namespace AmassOrchestrator.Web.Models;

public class ListSessionsResponse
{
    [JsonPropertyName("sessionTokens")]
    public List<string> SessionTokens { get; set; } = [];
}
