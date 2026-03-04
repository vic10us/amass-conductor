using AmassOrchestrator.Web.Models;

namespace AmassOrchestrator.Web.Services;

public interface IAmassEngineClient
{
    Task<HealthCheckResponse?> HealthCheckAsync(string podIp, int port, CancellationToken ct = default);
    Task<ListSessionsResponse?> ListSessionsAsync(string podIp, int port, CancellationToken ct = default);
    Task<SessionStatsResponse?> GetSessionStatsAsync(string podIp, int port, string sessionToken, CancellationToken ct = default);
    Task<CreateSessionResponse?> CreateSessionAsync(string podIp, int port, AmassConfig config, CancellationToken ct = default);
    Task<bool> DeleteSessionAsync(string podIp, int port, string sessionToken, CancellationToken ct = default);
    Task<ScopeResponse?> GetScopeAsync(string podIp, int port, string sessionToken, string assetType, CancellationToken ct = default);
    Task<AddAssetResponse?> AddAssetAsync(string podIp, int port, string sessionToken, string assetType, byte[] asset, CancellationToken ct = default);
    Task<BulkAddAssetsResponse?> BulkAddAssetsAsync(string podIp, int port, string sessionToken, string assetType, BulkAddAssetsRequest request, CancellationToken ct = default);
    IAsyncEnumerable<string> StreamLogsAsync(string podIp, int port, string sessionToken, CancellationToken ct = default);
}
