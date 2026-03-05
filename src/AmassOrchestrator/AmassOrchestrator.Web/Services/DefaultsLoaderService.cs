using System.Text.Json;
using AmassOrchestrator.Web.Configuration;
using AmassOrchestrator.Web.Models;
using Microsoft.Extensions.Options;

namespace AmassOrchestrator.Web.Services;

public class DefaultsLoaderService
{
    private readonly OrchestratorOptions _options;

    public DefaultsLoaderService(IOptions<OrchestratorOptions> options)
    {
        _options = options.Value;
    }

    public string GetBruteForceWordlist() => File.ReadAllText(_options.BruteForceWordlistFile);

    public string GetAlterationsWordlist() => File.ReadAllText(_options.AlterationsWordlistFile);

    public List<TransformationRow> GetDefaultTransformations()
    {
        var json = File.ReadAllText(_options.DefaultTransformationsFile);
        return JsonSerializer.Deserialize<List<TransformationRow>>(json, s_jsonOptions) ?? [];
    }

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
