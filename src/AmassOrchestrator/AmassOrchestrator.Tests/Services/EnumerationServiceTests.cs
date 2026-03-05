using AmassOrchestrator.Web.Configuration;
using AmassOrchestrator.Web.Models;
using AmassOrchestrator.Web.Models.Kubernetes;
using AmassOrchestrator.Web.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AmassOrchestrator.Tests.Services;

public class EnumerationServiceTests
{
    private readonly EngineStateStore _stateStore = new();
    private readonly Mock<IAmassEngineClient> _engineClient = new();
    private readonly IOptions<OrchestratorOptions> _options = Options.Create(new OrchestratorOptions());
    private readonly EnumerationService _sut;

    public EnumerationServiceTests()
    {
        _sut = new EnumerationService(
            _stateStore,
            _engineClient.Object,
            _options,
            Mock.Of<ILogger<EnumerationService>>());
    }

    private static EngineInstanceState MakeEngine(string name, int ordinal, bool healthy = true, bool ready = true, int sessionCount = 0)
    {
        var pod = new EnginePodInfo(name, $"10.0.0.{ordinal}", ordinal, "Running", ready);
        var sessions = Enumerable.Range(0, sessionCount)
            .Select(i => new SessionInfo(Guid.NewGuid().ToString(), 0, 0))
            .ToList();
        return new EngineInstanceState(pod, healthy, sessions);
    }

    private static AmassConfig MakeConfig(params string[] domains) =>
        new() { Scope = new AmassScope { Domains = domains.ToList() } };

    [Fact]
    public async Task StartEnumeration_FindsFreeEngine_CreatesSessionAndSubmitsAssets()
    {
        var engine = MakeEngine("engine-0", 0);
        _stateStore.UpdateState("engine-0", engine);

        _engineClient.Setup(c => c.CreateSessionAsync("10.0.0.0", 8080, It.IsAny<AmassConfig>(), default))
            .ReturnsAsync(new CreateSessionResponse { SessionToken = "token-123" });
        _engineClient.Setup(c => c.BulkAddAssetsAsync("10.0.0.0", 8080, "token-123", "fqdn", It.IsAny<BulkAddAssetsRequest>(), default))
            .ReturnsAsync(new BulkAddAssetsResponse { Ingested = 1 });

        var result = await _sut.StartEnumerationAsync(MakeConfig("example.com"));

        Assert.True(result.Success);
        Assert.Equal("token-123", result.SessionToken);
        Assert.Equal("engine-0", result.EnginePodName);
        _engineClient.Verify(c => c.BulkAddAssetsAsync("10.0.0.0", 8080, "token-123", "fqdn", It.IsAny<BulkAddAssetsRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task StartEnumeration_NoFreeEngine_ReturnsFailure()
    {
        // No engines registered
        var result = await _sut.StartEnumerationAsync(MakeConfig("example.com"));

        Assert.False(result.Success);
        Assert.Contains("No free engine", result.ErrorMessage);
    }

    [Fact]
    public async Task StartEnumeration_SkipsUnhealthyAndNotReadyEngines()
    {
        _stateStore.UpdateState("engine-0", MakeEngine("engine-0", 0, healthy: false));
        _stateStore.UpdateState("engine-1", MakeEngine("engine-1", 1, ready: false));
        _stateStore.UpdateState("engine-2", MakeEngine("engine-2", 2, sessionCount: 1));
        _stateStore.UpdateState("engine-3", MakeEngine("engine-3", 3));

        _engineClient.Setup(c => c.CreateSessionAsync("10.0.0.3", 8080, It.IsAny<AmassConfig>(), default))
            .ReturnsAsync(new CreateSessionResponse { SessionToken = "token-abc" });

        var result = await _sut.StartEnumerationAsync(MakeConfig("example.com"));

        Assert.True(result.Success);
        Assert.Equal("engine-3", result.EnginePodName);
    }

    [Fact]
    public async Task StartEnumeration_PicksLowestOrdinalFreeEngine()
    {
        _stateStore.UpdateState("engine-2", MakeEngine("engine-2", 2));
        _stateStore.UpdateState("engine-0", MakeEngine("engine-0", 0));
        _stateStore.UpdateState("engine-1", MakeEngine("engine-1", 1));

        _engineClient.Setup(c => c.CreateSessionAsync("10.0.0.0", 8080, It.IsAny<AmassConfig>(), default))
            .ReturnsAsync(new CreateSessionResponse { SessionToken = "token-first" });

        var result = await _sut.StartEnumerationAsync(MakeConfig("example.com"));

        Assert.True(result.Success);
        Assert.Equal("engine-0", result.EnginePodName);
    }

    [Fact]
    public async Task StartEnumerationOnEngine_EngineExists_CreatesSession()
    {
        var engine = MakeEngine("engine-0", 0);
        _stateStore.UpdateState("engine-0", engine);

        _engineClient.Setup(c => c.CreateSessionAsync("10.0.0.0", 8080, It.IsAny<AmassConfig>(), default))
            .ReturnsAsync(new CreateSessionResponse { SessionToken = "token-xyz" });

        var result = await _sut.StartEnumerationOnEngineAsync("engine-0", MakeConfig("example.com"));

        Assert.True(result.Success);
        Assert.Equal("token-xyz", result.SessionToken);
        Assert.Equal("engine-0", result.EnginePodName);
    }

    [Fact]
    public async Task StartEnumerationOnEngine_EngineNotFound_ReturnsFailure()
    {
        var result = await _sut.StartEnumerationOnEngineAsync("nonexistent", MakeConfig("example.com"));

        Assert.False(result.Success);
        Assert.Contains("not found", result.ErrorMessage);
    }

    [Fact]
    public async Task StartEnumeration_SessionCreationFails_ReturnsFailure()
    {
        _stateStore.UpdateState("engine-0", MakeEngine("engine-0", 0));

        _engineClient.Setup(c => c.CreateSessionAsync("10.0.0.0", 8080, It.IsAny<AmassConfig>(), default))
            .ReturnsAsync((CreateSessionResponse?)null);

        var result = await _sut.StartEnumerationAsync(MakeConfig("example.com"));

        Assert.False(result.Success);
        Assert.Contains("Failed to create session", result.ErrorMessage);
    }

    [Fact]
    public async Task StartEnumeration_AssetSubmissionFails_StillReturnsSuccess()
    {
        _stateStore.UpdateState("engine-0", MakeEngine("engine-0", 0));

        _engineClient.Setup(c => c.CreateSessionAsync("10.0.0.0", 8080, It.IsAny<AmassConfig>(), default))
            .ReturnsAsync(new CreateSessionResponse { SessionToken = "token-ok" });
        _engineClient.Setup(c => c.BulkAddAssetsAsync("10.0.0.0", 8080, "token-ok", "fqdn", It.IsAny<BulkAddAssetsRequest>(), default))
            .ReturnsAsync((BulkAddAssetsResponse?)null);

        var result = await _sut.StartEnumerationAsync(MakeConfig("example.com"));

        Assert.True(result.Success);
        Assert.Equal("token-ok", result.SessionToken);
    }

    [Fact]
    public async Task StartEnumeration_AssetSubmissionThrows_StillReturnsSuccess()
    {
        _stateStore.UpdateState("engine-0", MakeEngine("engine-0", 0));

        _engineClient.Setup(c => c.CreateSessionAsync("10.0.0.0", 8080, It.IsAny<AmassConfig>(), default))
            .ReturnsAsync(new CreateSessionResponse { SessionToken = "token-ok" });
        _engineClient.Setup(c => c.BulkAddAssetsAsync("10.0.0.0", 8080, "token-ok", "fqdn", It.IsAny<BulkAddAssetsRequest>(), default))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var result = await _sut.StartEnumerationAsync(MakeConfig("example.com"));

        Assert.True(result.Success);
        Assert.Equal("token-ok", result.SessionToken);
    }
}
