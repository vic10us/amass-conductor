using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AmassOrchestrator.Web.Configuration;
using AmassOrchestrator.Web.Models;
using Microsoft.Extensions.Options;

namespace AmassOrchestrator.Web.Services;

public class AmassEngineClient : IAmassEngineClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AmassEngineClient> _logger;
    private readonly OrchestratorOptions _options;

    public const string HttpClientName = "AmassEngine";

    private static readonly JsonSerializerOptions s_writeOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public AmassEngineClient(IHttpClientFactory httpClientFactory, ILogger<AmassEngineClient> logger, IOptions<OrchestratorOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = options.Value;
    }

    private string BaseUrl(string podIp, int port) => $"http://{podIp}:{port}{_options.EngineBasePath}";

    public async Task<HealthCheckResponse?> HealthCheckAsync(string podIp, int port, CancellationToken ct = default)
    {
        return await GetAsync<HealthCheckResponse>($"{BaseUrl(podIp, port)}/health", ct);
    }

    public async Task<ListSessionsResponse?> ListSessionsAsync(string podIp, int port, CancellationToken ct = default)
    {
        var url = $"{BaseUrl(podIp, port)}/sessions/list";
        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            var response = await client.GetAsync(url, ct);

            // The API returns 404 when zero sessions exist — treat as empty list, not an error.
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return new ListSessionsResponse();

            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<ListSessionsResponse>(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GET {Url} failed", url);
            return null;
        }
    }

    public async Task<SessionStatsResponse?> GetSessionStatsAsync(string podIp, int port, string sessionToken, CancellationToken ct = default)
    {
        return await GetAsync<SessionStatsResponse>($"{BaseUrl(podIp, port)}/sessions/{sessionToken}/stats", ct);
    }

    public async Task<CreateSessionResponse?> CreateSessionAsync(string podIp, int port, AmassConfig config, CancellationToken ct = default)
    {
        return await PostAsync<AmassConfig, CreateSessionResponse>($"{BaseUrl(podIp, port)}/sessions", config, ct);
    }

    public async Task<bool> DeleteSessionAsync(string podIp, int port, string sessionToken, CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            var response = await client.DeleteAsync($"{BaseUrl(podIp, port)}/sessions/{sessionToken}", ct);
            return response.StatusCode == System.Net.HttpStatusCode.NoContent;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete session {Token} on {PodIp}", sessionToken, podIp);
            return false;
        }
    }

    public async Task<ScopeResponse?> GetScopeAsync(string podIp, int port, string sessionToken, string assetType, CancellationToken ct = default)
    {
        return await GetAsync<ScopeResponse>($"{BaseUrl(podIp, port)}/sessions/{sessionToken}/scope/{assetType}", ct);
    }

    public async Task<AddAssetResponse?> AddAssetAsync(string podIp, int port, string sessionToken, string assetType, byte[] asset, CancellationToken ct = default)
    {
        return await PostAsync<byte[], AddAssetResponse>(
            $"{BaseUrl(podIp, port)}/sessions/{sessionToken}/assets/{assetType}", asset, ct);
    }

    public async Task<BulkAddAssetsResponse?> BulkAddAssetsAsync(string podIp, int port, string sessionToken, string assetType, BulkAddAssetsRequest request, CancellationToken ct = default)
    {
        return await PostAsync<BulkAddAssetsRequest, BulkAddAssetsResponse>(
            $"{BaseUrl(podIp, port)}/sessions/{sessionToken}/assets/{assetType}:bulk", request, ct);
    }

    public async IAsyncEnumerable<string> StreamLogsAsync(
        string podIp, int port, string sessionToken,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var uri = new Uri($"ws://{podIp}:{port}{_options.EngineBasePath}/sessions/{sessionToken}/ws/logs");
        using var ws = new ClientWebSocket();

        try
        {
            await ws.ConnectAsync(uri, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WebSocket connect failed for {Uri}", uri);
            yield break;
        }

        var buffer = new byte[_options.WebSocketBufferSize];

        while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            WebSocketReceiveResult result;
            try
            {
                result = await ws.ReceiveAsync(buffer, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (WebSocketException ex)
            {
                _logger.LogWarning(ex, "WebSocket read error for session {Token}", sessionToken);
                break;
            }

            if (result.MessageType == WebSocketMessageType.Close)
                break;

            if (result.MessageType == WebSocketMessageType.Text)
                yield return Encoding.UTF8.GetString(buffer, 0, result.Count);
        }
    }

    private async Task<T?> GetAsync<T>(string url, CancellationToken ct) where T : class
    {
        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<T>(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GET {Url} failed", url);
            return null;
        }
    }

    private async Task<TResponse?> PostAsync<TRequest, TResponse>(string url, TRequest body, CancellationToken ct)
        where TResponse : class
    {
        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            var json = JsonSerializer.Serialize(body, s_writeOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(url, content, ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<TResponse>(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "POST {Url} failed", url);
            return null;
        }
    }
}
