using System.Net;
using System.Text.Json;
using AmassOrchestrator.Web.Configuration;
using AmassOrchestrator.Web.Models;
using AmassOrchestrator.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;

namespace AmassOrchestrator.Tests.Services;

public class AmassEngineClientTests
{
    private static AmassEngineClient CreateClient(HttpMessageHandler handler)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(AmassEngineClient.HttpClientName))
            .Returns(new HttpClient(handler));
        var options = Options.Create(new OrchestratorOptions());
        return new AmassEngineClient(factory.Object, NullLogger<AmassEngineClient>.Instance, options);
    }

    private static Mock<HttpMessageHandler> CreateMockHandler(HttpStatusCode statusCode, object? content = null)
    {
        var handler = new Mock<HttpMessageHandler>();
        var response = new HttpResponseMessage(statusCode);
        if (content != null)
            response.Content = new StringContent(JsonSerializer.Serialize(content), System.Text.Encoding.UTF8, "application/json");

        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        return handler;
    }

    [Fact]
    public async Task HealthCheckAsync_ReturnsResponse_OnSuccess()
    {
        var expected = new HealthCheckResponse { Result = "ok" };
        var handler = CreateMockHandler(HttpStatusCode.OK, expected);
        var client = CreateClient(handler.Object);

        var result = await client.HealthCheckAsync("10.0.0.1", 8080);

        Assert.NotNull(result);
        Assert.Equal("ok", result!.Result);
    }

    [Fact]
    public async Task HealthCheckAsync_ReturnsNull_OnFailure()
    {
        var handler = CreateMockHandler(HttpStatusCode.InternalServerError);
        var client = CreateClient(handler.Object);

        var result = await client.HealthCheckAsync("10.0.0.1", 8080);

        Assert.Null(result);
    }

    [Fact]
    public async Task ListSessionsAsync_ReturnsSessions()
    {
        var expected = new ListSessionsResponse { SessionTokens = ["abc-123", "def-456"] };
        var handler = CreateMockHandler(HttpStatusCode.OK, expected);
        var client = CreateClient(handler.Object);

        var result = await client.ListSessionsAsync("10.0.0.1", 8080);

        Assert.NotNull(result);
        Assert.Equal(2, result!.SessionTokens.Count);
    }

    [Fact]
    public async Task ListSessionsAsync_ReturnsEmptyList_On404()
    {
        var handler = CreateMockHandler(HttpStatusCode.NotFound);
        var client = CreateClient(handler.Object);

        var result = await client.ListSessionsAsync("10.0.0.1", 8080);

        Assert.NotNull(result);
        Assert.Empty(result!.SessionTokens);
    }

    [Fact]
    public async Task GetSessionStatsAsync_ReturnsStats()
    {
        var expected = new SessionStatsResponse { WorkItemsCompleted = 5, WorkItemsTotal = 10 };
        var handler = CreateMockHandler(HttpStatusCode.OK, expected);
        var client = CreateClient(handler.Object);

        var result = await client.GetSessionStatsAsync("10.0.0.1", 8080, "token-1");

        Assert.NotNull(result);
        Assert.Equal(5, result!.WorkItemsCompleted);
        Assert.Equal(10, result.WorkItemsTotal);
    }

    [Fact]
    public async Task DeleteSessionAsync_ReturnsTrue_OnNoContent()
    {
        var handler = CreateMockHandler(HttpStatusCode.NoContent);
        var client = CreateClient(handler.Object);

        var result = await client.DeleteSessionAsync("10.0.0.1", 8080, "token-1");

        Assert.True(result);
    }

    [Fact]
    public async Task DeleteSessionAsync_ReturnsFalse_OnError()
    {
        var handler = CreateMockHandler(HttpStatusCode.NotFound);
        var client = CreateClient(handler.Object);

        var result = await client.DeleteSessionAsync("10.0.0.1", 8080, "token-1");

        Assert.False(result);
    }

    [Fact]
    public async Task CreateSessionAsync_ReturnsToken()
    {
        var expected = new CreateSessionResponse { SessionToken = "new-token" };
        var handler = CreateMockHandler(HttpStatusCode.Created, expected);

        // Created returns 201 which isn't a success code by default... actually it is.
        // Let's use OK for simplicity in mock
        var handler2 = CreateMockHandler(HttpStatusCode.OK, expected);
        var client = CreateClient(handler2.Object);

        var config = new AmassConfig { Scope = new AmassScope { Domains = ["example.com"] } };
        var result = await client.CreateSessionAsync("10.0.0.1", 8080, config);

        Assert.NotNull(result);
        Assert.Equal("new-token", result!.SessionToken);
    }

    [Fact]
    public async Task HealthCheckAsync_ReturnsNull_OnException()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var client = CreateClient(handler.Object);
        var result = await client.HealthCheckAsync("10.0.0.1", 8080);

        Assert.Null(result);
    }
}
