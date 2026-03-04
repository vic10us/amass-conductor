using System.Text.Json.Serialization;

namespace AmassOrchestrator.Web.Models;

public class CreateSessionResponse
{
    [JsonPropertyName("sessionToken")]
    public string SessionToken { get; set; } = string.Empty;
}
