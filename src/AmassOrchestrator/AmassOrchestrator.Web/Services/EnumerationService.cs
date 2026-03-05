using System.Text.Json;
using AmassOrchestrator.Web.Configuration;
using AmassOrchestrator.Web.Models;
using AmassOrchestrator.Web.Models.Kubernetes;
using Microsoft.Extensions.Options;

namespace AmassOrchestrator.Web.Services;

public class EnumerationService(
    EngineStateStore stateStore,
    IAmassEngineClient engineClient,
    IOptions<OrchestratorOptions> options,
    ILogger<EnumerationService> logger,
    ISessionRepository sessionRepository) : IEnumerationService
{
    public async Task<EnumerationResult> StartEnumerationAsync(AmassConfig config)
    {
        var maxActive = options.Value.MaxActiveSessionsPerEngine;

        var candidate = stateStore.States.Values
            .Where(s => s.IsHealthy && s.Pod.IsReady)
            .Where(s => s.Sessions.Count(sess => !sess.IsCompleted) < maxActive)
            .OrderBy(s => s.Sessions.Count(sess => !sess.IsCompleted))
            .ThenBy(s => s.Sessions.Count)
            .ThenBy(s => s.Pod.Ordinal)
            .FirstOrDefault();

        if (candidate is null)
        {
            logger.LogWarning("No free engine available for enumeration");
            return EnumerationResult.Fail("No free engine available. All engines are busy or unhealthy.");
        }

        return await RunEnumerationAsync(candidate, config);
    }

    public async Task<EnumerationResult> StartEnumerationOnEngineAsync(string podName, AmassConfig config)
    {
        var state = stateStore.GetState(podName);
        if (state is null)
        {
            logger.LogWarning("Engine {PodName} not found", podName);
            return EnumerationResult.Fail($"Engine '{podName}' not found.");
        }

        return await RunEnumerationAsync(state, config);
    }

    private async Task<EnumerationResult> RunEnumerationAsync(EngineInstanceState engine, AmassConfig config)
    {
        var port = options.Value.EnginePort;
        var podIp = engine.Pod.PodIP;
        var podName = engine.Pod.PodName;

        var sessionResponse = await engineClient.CreateSessionAsync(podIp, port, config);
        if (sessionResponse is null)
        {
            logger.LogError("Failed to create session on engine {PodName}", podName);
            return EnumerationResult.Fail($"Failed to create session on engine '{podName}'.");
        }

        var sessionToken = sessionResponse.SessionToken;
        var domains = config.Scope?.Domains ?? [];

        // Submit FQDN assets — failure is non-fatal since the session already exists
        try
        {
            var items = domains
                .Select(d => (object)new { name = d })
                .ToList();

            var bulkRequest = new BulkAddAssetsRequest { Items = items };
            var bulkResponse = await engineClient.BulkAddAssetsAsync(podIp, port, sessionToken, "fqdn", bulkRequest);

            if (bulkResponse is null)
            {
                logger.LogWarning("Asset submission returned null for session {Token} on {PodName}, but session exists", sessionToken, podName);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Asset submission failed for session {Token} on {PodName}, but session exists", sessionToken, podName);
        }

        logger.LogInformation("Enumeration started on {PodName} with session {Token} for domains: {Domains}",
            podName, sessionToken, string.Join(", ", domains));

        try
        {
            var configJson = JsonSerializer.Serialize(config);
            await sessionRepository.CreateAsync(podName, sessionToken, domains, configJson);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist new session {Token} to database", sessionToken);
        }

        return EnumerationResult.Ok(sessionToken, podName);
    }
}
