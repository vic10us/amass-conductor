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
    public async Task<EnumerationResult> StartEnumerationAsync(AmassConfig config, SeedAssets? assets = null, bool submitDomainsAsFqdns = true)
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

        return await RunEnumerationAsync(candidate, config, assets, submitDomainsAsFqdns);
    }

    public async Task<EnumerationResult> StartEnumerationOnEngineAsync(string podName, AmassConfig config, SeedAssets? assets = null, bool submitDomainsAsFqdns = true)
    {
        var state = stateStore.GetState(podName);
        if (state is null)
        {
            logger.LogWarning("Engine {PodName} not found", podName);
            return EnumerationResult.Fail($"Engine '{podName}' not found.");
        }

        return await RunEnumerationAsync(state, config, assets, submitDomainsAsFqdns);
    }

    private async Task<EnumerationResult> RunEnumerationAsync(EngineInstanceState engine, AmassConfig config, SeedAssets? assets, bool submitDomainsAsFqdns)
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

        await SubmitSeedAssetsAsync(podIp, port, podName, sessionToken, domains, assets, submitDomainsAsFqdns);

        logger.LogInformation("Enumeration started on {PodName} with session {Token} for domains: {Domains}",
            podName, sessionToken, string.Join(", ", domains));

        try
        {
            var persistedData = new PersistedSessionData
            {
                Config = config,
                Assets = assets,
                SubmitDomainsAsFqdns = submitDomainsAsFqdns
            };
            var configJson = persistedData.Serialize();
            await sessionRepository.CreateAsync(podName, sessionToken, domains, configJson);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist new session {Token} to database", sessionToken);
        }

        return EnumerationResult.Ok(sessionToken, podName);
    }

    private async Task SubmitSeedAssetsAsync(string podIp, int port, string podName, string sessionToken,
        List<string> domains, SeedAssets? assets, bool submitDomainsAsFqdns)
    {
        // FQDNs: merge scope domains (when toggle is on) with explicit FQDN assets
        var fqdns = new List<string>();
        if (submitDomainsAsFqdns)
            fqdns.AddRange(domains);
        if (assets?.Fqdns.Count > 0)
            fqdns.AddRange(assets.Fqdns);

        var dedupedFqdns = fqdns.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (dedupedFqdns.Count > 0)
        {
            var items = dedupedFqdns.Select(d => (object)new { name = d }).ToList();
            await SubmitBulkAsync(podIp, port, podName, sessionToken, "fqdn", items);
        }

        if (assets is null)
            return;

        // IP Addresses
        if (assets.IpAddresses.Count > 0)
        {
            var items = assets.IpAddresses
                .Select(ip => (object)new { address = ip, type = ip.Contains(':') ? "IPv6" : "IPv4" })
                .ToList();
            await SubmitBulkAsync(podIp, port, podName, sessionToken, "ipaddress", items);
        }

        // Autonomous Systems
        if (assets.AutonomousSystems.Count > 0)
        {
            var items = assets.AutonomousSystems
                .Select(asn => (object)new { number = asn })
                .ToList();
            await SubmitBulkAsync(podIp, port, podName, sessionToken, "autonomoussystem", items);
        }

        // Netblocks
        if (assets.Netblocks.Count > 0)
        {
            var items = assets.Netblocks
                .Select(nb => (object)new { cidr = nb, type = nb.Contains(':') ? "IPv6" : "IPv4" })
                .ToList();
            await SubmitBulkAsync(podIp, port, podName, sessionToken, "netblock", items);
        }

        // Organizations
        if (assets.Organizations.Count > 0)
        {
            var items = assets.Organizations
                .Select(org => (object)new { name = org })
                .ToList();
            await SubmitBulkAsync(podIp, port, podName, sessionToken, "organization", items);
        }

        // Locations
        if (assets.Locations.Count > 0)
        {
            var items = assets.Locations
                .Select(loc => (object)new { address = loc })
                .ToList();
            await SubmitBulkAsync(podIp, port, podName, sessionToken, "location", items);
        }
    }

    private async Task SubmitBulkAsync(string podIp, int port, string podName, string sessionToken,
        string assetType, List<object> items)
    {
        try
        {
            var bulkRequest = new BulkAddAssetsRequest { Items = items };
            var bulkResponse = await engineClient.BulkAddAssetsAsync(podIp, port, sessionToken, assetType, bulkRequest);

            if (bulkResponse is null)
            {
                logger.LogWarning("{AssetType} submission returned null for session {Token} on {PodName}, but session exists",
                    assetType, sessionToken, podName);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{AssetType} submission failed for session {Token} on {PodName}, but session exists",
                assetType, sessionToken, podName);
        }
    }
}
