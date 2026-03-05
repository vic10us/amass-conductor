using AmassOrchestrator.Web.Models;

namespace AmassOrchestrator.Web.Services;

public interface IEnumerationService
{
    Task<EnumerationResult> StartEnumerationAsync(AmassConfig config);
    Task<EnumerationResult> StartEnumerationOnEngineAsync(string podName, AmassConfig config);
}
