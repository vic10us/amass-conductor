using System.Text.Json.Serialization;

namespace AmassOrchestrator.Web.Models;

public class BulkAddAssetsResponse
{
    [JsonPropertyName("failed")]
    public int Failed { get; set; }

    [JsonPropertyName("ingested")]
    public int Ingested { get; set; }

    [JsonPropertyName("stored")]
    public int Stored { get; set; }
}
