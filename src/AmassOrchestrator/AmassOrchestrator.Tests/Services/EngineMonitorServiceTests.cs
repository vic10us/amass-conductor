using AmassOrchestrator.Web.Configuration;
using AmassOrchestrator.Web.Models;
using AmassOrchestrator.Web.Models.Kubernetes;
using AmassOrchestrator.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace AmassOrchestrator.Tests.Services;

public class EngineMonitorServiceTests
{
    private static IOptionsMonitor<OrchestratorOptions> DefaultOptions()
    {
        var mock = new Mock<IOptionsMonitor<OrchestratorOptions>>();
        mock.Setup(m => m.CurrentValue).Returns(new OrchestratorOptions
        {
            Namespace = "default",
            StatefulSetName = "amass-engine",
            EnginePort = 8080,
            PollIntervalSeconds = 5
        });
        return mock.Object;
    }

    [Fact]
    public async Task PollAsync_UpdatesStateStore_WithDiscoveredPods()
    {
        var pods = new List<EnginePodInfo>
        {
            new("amass-engine-0", "10.0.0.1", 0, "Running", true)
        };

        var discoveryMock = new Mock<IKubernetesDiscoveryService>();
        discoveryMock.Setup(d => d.DiscoverEnginePodsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(pods);

        var clientMock = new Mock<IAmassEngineClient>();
        clientMock.Setup(c => c.HealthCheckAsync("10.0.0.1", 8080, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HealthCheckResponse { Result = "ok" });
        clientMock.Setup(c => c.ListSessionsAsync("10.0.0.1", 8080, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListSessionsResponse { SessionTokens = ["token-1"] });
        clientMock.Setup(c => c.GetSessionStatsAsync("10.0.0.1", 8080, "token-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionStatsResponse { WorkItemsCompleted = 3, WorkItemsTotal = 10 });

        var stateStore = new EngineStateStore();

        var service = new EngineMonitorService(
            discoveryMock.Object, clientMock.Object, stateStore,
            DefaultOptions(), NullLogger<EngineMonitorService>.Instance);

        await service.PollAsync(CancellationToken.None);

        var state = stateStore.GetState("amass-engine-0");
        Assert.NotNull(state);
        Assert.True(state!.IsHealthy);
        Assert.Single(state.Sessions);
        Assert.Equal("token-1", state.Sessions[0].Token);
        Assert.Equal(3, state.Sessions[0].WorkItemsCompleted);
        Assert.Equal(10, state.Sessions[0].WorkItemsTotal);
    }

    [Fact]
    public async Task PollAsync_RemovesStalePods()
    {
        var stateStore = new EngineStateStore();
        stateStore.UpdateState("amass-engine-0",
            new EngineInstanceState(
                new EnginePodInfo("amass-engine-0", "10.0.0.1", 0, "Running", true),
                true, []));
        stateStore.UpdateState("amass-engine-1",
            new EngineInstanceState(
                new EnginePodInfo("amass-engine-1", "10.0.0.2", 1, "Running", true),
                true, []));

        // Only pod-0 discovered now
        var pods = new List<EnginePodInfo>
        {
            new("amass-engine-0", "10.0.0.1", 0, "Running", true)
        };

        var discoveryMock = new Mock<IKubernetesDiscoveryService>();
        discoveryMock.Setup(d => d.DiscoverEnginePodsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(pods);

        var clientMock = new Mock<IAmassEngineClient>();
        clientMock.Setup(c => c.HealthCheckAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HealthCheckResponse { Result = "ok" });
        clientMock.Setup(c => c.ListSessionsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListSessionsResponse { SessionTokens = [] });

        var service = new EngineMonitorService(
            discoveryMock.Object, clientMock.Object, stateStore,
            DefaultOptions(), NullLogger<EngineMonitorService>.Instance);

        await service.PollAsync(CancellationToken.None);

        Assert.NotNull(stateStore.GetState("amass-engine-0"));
        Assert.Null(stateStore.GetState("amass-engine-1"));
    }

    [Fact]
    public async Task PollAsync_HandlesUnhealthyPod()
    {
        var pods = new List<EnginePodInfo>
        {
            new("amass-engine-0", "10.0.0.1", 0, "Running", true)
        };

        var discoveryMock = new Mock<IKubernetesDiscoveryService>();
        discoveryMock.Setup(d => d.DiscoverEnginePodsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(pods);

        var clientMock = new Mock<IAmassEngineClient>();
        clientMock.Setup(c => c.HealthCheckAsync("10.0.0.1", 8080, It.IsAny<CancellationToken>()))
            .ReturnsAsync((HealthCheckResponse?)null);
        clientMock.Setup(c => c.ListSessionsAsync("10.0.0.1", 8080, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ListSessionsResponse?)null);

        var stateStore = new EngineStateStore();

        var service = new EngineMonitorService(
            discoveryMock.Object, clientMock.Object, stateStore,
            DefaultOptions(), NullLogger<EngineMonitorService>.Instance);

        await service.PollAsync(CancellationToken.None);

        var state = stateStore.GetState("amass-engine-0");
        Assert.NotNull(state);
        Assert.False(state!.IsHealthy);
        Assert.Empty(state.Sessions);
    }
}
