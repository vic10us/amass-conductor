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
    public async Task<EnumerationResult> StartEnumerationAsync(AmassConfig config, SeedAssets? assets = null, bool submitDomainsAsFqdns = true, IProgress<EnumerationProgress>? progress = null)
    {
        progress?.Report(new EnumerationProgress("Selecting engine...", 0));

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

        return await RunEnumerationAsync(candidate, config, assets, submitDomainsAsFqdns, progress);
    }

    public async Task<EnumerationResult> StartEnumerationOnEngineAsync(string podName, AmassConfig config, SeedAssets? assets = null, bool submitDomainsAsFqdns = true, IProgress<EnumerationProgress>? progress = null)
    {
        var state = stateStore.GetState(podName);
        if (state is null)
        {
            logger.LogWarning("Engine {PodName} not found", podName);
            return EnumerationResult.Fail($"Engine '{podName}' not found.");
        }

        return await RunEnumerationAsync(state, config, assets, submitDomainsAsFqdns, progress);
    }

    private async Task<EnumerationResult> RunEnumerationAsync(EngineInstanceState engine, AmassConfig config, SeedAssets? assets, bool submitDomainsAsFqdns, IProgress<EnumerationProgress>? progress)
    {
        var port = options.Value.EnginePort;
        var podIp = engine.Pod.PodIP;
        var podName = engine.Pod.PodName;

        progress?.Report(new EnumerationProgress($"Creating session on {podName}...", 5));

        var sessionResponse = await engineClient.CreateSessionAsync(podIp, port, config);
        if (sessionResponse is null)
        {
            logger.LogError("Failed to create session on engine {PodName}", podName);
            return EnumerationResult.Fail($"Failed to create session on engine '{podName}'. The engine did not respond or returned an error.");
        }

        var sessionToken = sessionResponse.SessionToken;
        var domains = config.Scope?.Domains ?? [];

        try
        {
            await SubmitSeedAssetsAsync(podIp, port, podName, sessionToken, domains, assets, submitDomainsAsFqdns, progress);
        }
        catch (AssetSubmissionException ex)
        {
            logger.LogError(ex, "Asset submission failed for session {Token} on {PodName}, cleaning up", sessionToken, podName);

            try
            {
                await engineClient.DeleteSessionAsync(podIp, port, sessionToken);
                logger.LogInformation("Cleaned up failed session {Token} from engine {PodName}", sessionToken, podName);
            }
            catch (Exception cleanupEx)
            {
                logger.LogWarning(cleanupEx, "Failed to clean up session {Token} from engine {PodName}", sessionToken, podName);
            }

            return EnumerationResult.Fail(ex.Message);
        }

        progress?.Report(new EnumerationProgress("Saving session...", 95));

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

        progress?.Report(new EnumerationProgress("Session started successfully.", 100));

        return EnumerationResult.Ok(sessionToken, podName);
    }

    private static readonly Dictionary<string, string> AssetTypeLabels = new()
    {
        ["fqdn"] = "FQDNs",
        ["ipaddress"] = "IP addresses",
        ["autonomoussystem"] = "autonomous systems",
        ["netblock"] = "netblocks",
        ["organization"] = "organizations",
        ["location"] = "locations"
    };

    private async Task SubmitSeedAssetsAsync(string podIp, int port, string podName, string sessionToken,
        List<string> domains, SeedAssets? assets, bool submitDomainsAsFqdns, IProgress<EnumerationProgress>? progress)
    {
        // Build all asset groups to submit
        var submissions = new List<(string AssetType, List<object> Items)>();

        // FQDNs: merge scope domains (when toggle is on) with explicit FQDN assets
        var fqdns = new List<string>();
        if (submitDomainsAsFqdns)
            fqdns.AddRange(domains);
        if (assets?.Fqdns.Count > 0)
            fqdns.AddRange(assets.Fqdns);

        var dedupedFqdns = fqdns.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (dedupedFqdns.Count > 0)
            submissions.Add(("fqdn", dedupedFqdns.Select(d => (object)new { name = d }).ToList()));

        if (assets is not null)
        {
            if (assets.IpAddresses.Count > 0)
                submissions.Add(("ipaddress", assets.IpAddresses
                    .Select(ip => (object)new { address = ip, type = ip.Contains(':') ? "IPv6" : "IPv4" }).ToList()));

            if (assets.AutonomousSystems.Count > 0)
                submissions.Add(("autonomoussystem", assets.AutonomousSystems
                    .Select(asn => (object)new { number = asn }).ToList()));

            if (assets.Netblocks.Count > 0)
                submissions.Add(("netblock", assets.Netblocks
                    .Select(nb => (object)new { cidr = nb, type = nb.Contains(':') ? "IPv6" : "IPv4" }).ToList()));

            if (assets.Organizations.Count > 0)
                submissions.Add(("organization", assets.Organizations
                    .Select(org => (object)new { name = org }).ToList()));

            if (assets.Locations.Count > 0)
                submissions.Add(("location", assets.Locations
                    .Select(loc => (object)new { address = loc }).ToList()));
        }

        if (submissions.Count == 0)
            return;

        var totalItems = submissions.Sum(s => s.Items.Count);
        var submittedItems = 0;

        foreach (var (assetType, items) in submissions)
        {
            var label = AssetTypeLabels.GetValueOrDefault(assetType, assetType);
            submittedItems = await SubmitBulkAsync(podIp, port, podName, sessionToken, assetType, label, items, totalItems, submittedItems, progress);
        }
    }

    private int MaxBatchSize => options.Value.BulkAssetBatchSize;

    private async Task<int> SubmitBulkAsync(string podIp, int port, string podName, string sessionToken,
        string assetType, string label, List<object> items, int totalItems, int submittedItems, IProgress<EnumerationProgress>? progress)
    {
        var maxBatchSize = MaxBatchSize;
        if (items.Count <= maxBatchSize)
        {
            ReportAssetProgress(progress, label, items.Count, items.Count, totalItems, submittedItems);
            await SubmitBulkBatchAsync(podIp, port, podName, sessionToken, assetType, label, items);
            return submittedItems + items.Count;
        }

        var totalBatches = (int)Math.Ceiling((double)items.Count / maxBatchSize);
        logger.LogInformation("Chunking {AssetType} submission of {TotalCount} items into {BatchCount} batches for session {Token} on {PodName}",
            assetType, items.Count, totalBatches, sessionToken, podName);

        var baseSubmitted = submittedItems;
        var batchSubmitted = 0;
        for (var i = 0; i < items.Count; i += maxBatchSize)
        {
            var batch = items.GetRange(i, Math.Min(maxBatchSize, items.Count - i));
            batchSubmitted += batch.Count;
            ReportAssetProgress(progress, label, batchSubmitted, items.Count, totalItems, baseSubmitted);
            await SubmitBulkBatchAsync(podIp, port, podName, sessionToken, assetType, label, batch);
        }
        submittedItems = baseSubmitted + items.Count;

        return submittedItems;
    }

    private void ReportAssetProgress(IProgress<EnumerationProgress>? progress, string label, int current, int typeTotal, int grandTotal, int alreadySubmitted)
    {
        if (progress is null) return;

        // Progress range: 10% (after session created) to 90% (before saving)
        var fraction = grandTotal > 0 ? (double)(alreadySubmitted + current) / grandTotal : 1.0;
        var percent = 10 + (int)(fraction * 80);

        var message = typeTotal > MaxBatchSize
            ? $"Submitting {label} ({current:N0} of {typeTotal:N0})..."
            : $"Submitting {typeTotal:N0} {label}...";

        progress.Report(new EnumerationProgress(message, percent));
    }

    private async Task SubmitBulkBatchAsync(string podIp, int port, string podName, string sessionToken,
        string assetType, string label, List<object> items)
    {
        try
        {
            var bulkRequest = new BulkAddAssetsRequest { Items = items };
            var bulkResponse = await engineClient.BulkAddAssetsAsync(podIp, port, sessionToken, assetType, bulkRequest);

            if (bulkResponse is null)
            {
                throw new AssetSubmissionException(
                    $"Failed to submit {items.Count:N0} {label} to engine '{podName}'. The engine did not respond.");
            }
        }
        catch (AssetSubmissionException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new AssetSubmissionException(
                $"Failed to submit {items.Count:N0} {label} to engine '{podName}'. Network error: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new AssetSubmissionException(
                $"Failed to submit {items.Count:N0} {label} to engine '{podName}'. The request timed out.", ex);
        }
        catch (Exception ex)
        {
            throw new AssetSubmissionException(
                $"Failed to submit {items.Count:N0} {label} to engine '{podName}'. {ex.Message}", ex);
        }
    }

    private class AssetSubmissionException(string message, Exception? innerException = null)
        : Exception(message, innerException);
}
