using System.Text.Json.Serialization;

namespace AmassOrchestrator.Web.Models;

public class BulkAddAssetsRequest
{
    [JsonPropertyName("items")]
    public List<byte[]> Items { get; set; } = [];
}
