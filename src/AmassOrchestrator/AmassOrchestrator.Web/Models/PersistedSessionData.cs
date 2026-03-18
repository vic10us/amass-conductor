using System.Text.Json;
using System.Text.Json.Serialization;

namespace AmassOrchestrator.Web.Models;

public class PersistedSessionData
{
    [JsonPropertyName("config")]
    public AmassConfig Config { get; set; } = new();

    [JsonPropertyName("assets")]
    public SeedAssets? Assets { get; set; }

    [JsonPropertyName("submit_domains_as_fqdns")]
    public bool SubmitDomainsAsFqdns { get; set; } = true;

    private static readonly JsonSerializerOptions SerializeOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string Serialize() => JsonSerializer.Serialize(this, SerializeOptions);

    /// <summary>
    /// Deserializes config JSON, handling both the new wrapper format and
    /// legacy format (bare AmassConfig).
    /// </summary>
    public static PersistedSessionData Deserialize(string json)
    {
        try
        {
            var data = JsonSerializer.Deserialize<PersistedSessionData>(json);
            // If the "config" property was present and has scope/domains, it's the new format
            if (data?.Config?.Scope != null || data?.Config?.Active != null ||
                data?.Config?.BruteForce != null || data?.Config?.Database != null)
            {
                return data;
            }
        }
        catch
        {
            // Fall through to legacy parsing
        }

        // Legacy format: bare AmassConfig
        var config = JsonSerializer.Deserialize<AmassConfig>(json);
        return new PersistedSessionData { Config = config ?? new AmassConfig() };
    }
}
