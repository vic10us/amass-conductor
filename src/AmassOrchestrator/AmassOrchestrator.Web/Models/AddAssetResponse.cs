using System.Text.Json.Serialization;

namespace AmassOrchestrator.Web.Models;

public class AddAssetResponse
{
    [JsonPropertyName("entityID")]
    public string EntityId { get; set; } = string.Empty;
}
