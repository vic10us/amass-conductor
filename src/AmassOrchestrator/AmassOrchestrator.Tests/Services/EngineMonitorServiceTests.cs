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

    private static EngineMonitorService CreateService(
        Mock<IKubernetesDiscoveryService> discoveryMock,
        Mock<IAmassEngineClient> clientMock,
        EngineStateStore stateStore) =>
        new(discoveryMock.Object, clientMock.Object, stateStore,
            DefaultOptions(), NullLogger<EngineMonitorService>.Instance, Mock.Of<ISessionRepository>());

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
            .ReturnsAsync(new HealthCheckResponse { Result = "Amass Engine OK" });
        clientMock.Setup(c => c.ListSessionsAsync("10.0.0.1", 8080, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListSessionsResponse { SessionTokens = ["token-1"] });
        clientMock.Setup(c => c.GetSessionStatsAsync("10.0.0.1", 8080, "token-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionStatsResponse { WorkItemsCompleted = 3, WorkItemsTotal = 10 });

        var stateStore = new EngineStateStore();
        var service = CreateService(discoveryMock, clientMock, stateStore);

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
            .ReturnsAsync(new HealthCheckResponse { Result = "Amass Engine OK" });
        clientMock.Setup(c => c.ListSessionsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListSessionsResponse { SessionTokens = [] });

        var service = CreateService(discoveryMock, clientMock, stateStore);

        await service.PollAsync(CancellationToken.None);

        Assert.NotNull(stateStore.GetState("amass-engine-0"));
        Assert.Null(stateStore.GetState("amass-engine-1"));
    }

    [Fact]
    public async Task PollAsync_MarksSessionCompleted_After5ConsecutiveCompletionPolls()
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
            .ReturnsAsync(new HealthCheckResponse { Result = "Amass Engine OK" });
        clientMock.Setup(c => c.ListSessionsAsync("10.0.0.1", 8080, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListSessionsResponse { SessionTokens = ["token-1"] });
        clientMock.Setup(c => c.GetSessionStatsAsync("10.0.0.1", 8080, "token-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionStatsResponse { WorkItemsCompleted = 10, WorkItemsTotal = 10 });

        var stateStore = new EngineStateStore();
        var service = CreateService(discoveryMock, clientMock, stateStore);

        // Poll 5 times — should become completed on the 5th
        for (var i = 0; i < 5; i++)
        {
            await service.PollAsync(CancellationToken.None);
            var state = stateStore.GetState("amass-engine-0")!;
            if (i < 4)
            {
                Assert.False(state.Sessions[0].IsCompleted);
                Assert.Equal(i + 1, state.Sessions[0].ConsecutiveCompletionPolls);
            }
            else
            {
                Assert.True(state.Sessions[0].IsCompleted);
            }
        }
    }

    [Fact]
    public async Task PollAsync_ResetsCompletionCounter_WhenProgressRegresses()
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
            .ReturnsAsync(new HealthCheckResponse { Result = "Amass Engine OK" });
        clientMock.Setup(c => c.ListSessionsAsync("10.0.0.1", 8080, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListSessionsResponse { SessionTokens = ["token-1"] });

        var stateStore = new EngineStateStore();
        var service = CreateService(discoveryMock, clientMock, stateStore);

        // 3 polls with completed == total
        clientMock.Setup(c => c.GetSessionStatsAsync("10.0.0.1", 8080, "token-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionStatsResponse { WorkItemsCompleted = 10, WorkItemsTotal = 10 });

        for (var i = 0; i < 3; i++)
        {
            await service.PollAsync(CancellationToken.None);
        }

        Assert.Equal(3, stateStore.GetState("amass-engine-0")!.Sessions[0].ConsecutiveCompletionPolls);
        Assert.False(stateStore.GetState("amass-engine-0")!.Sessions[0].IsCompleted);

        // Regression: more work items appear
        clientMock.Setup(c => c.GetSessionStatsAsync("10.0.0.1", 8080, "token-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionStatsResponse { WorkItemsCompleted = 10, WorkItemsTotal = 15 });

        await service.PollAsync(CancellationToken.None);

        Assert.Equal(0, stateStore.GetState("amass-engine-0")!.Sessions[0].ConsecutiveCompletionPolls);
        Assert.False(stateStore.GetState("amass-engine-0")!.Sessions[0].IsCompleted);
    }

    [Fact]
    public async Task PollAsync_FirstPoll_HasZeroItemsPerSecond()
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
            .ReturnsAsync(new HealthCheckResponse { Result = "Amass Engine OK" });
        clientMock.Setup(c => c.ListSessionsAsync("10.0.0.1", 8080, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListSessionsResponse { SessionTokens = ["token-1"] });
        clientMock.Setup(c => c.GetSessionStatsAsync("10.0.0.1", 8080, "token-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionStatsResponse { WorkItemsCompleted = 5, WorkItemsTotal = 100 });

        var stateStore = new EngineStateStore();
        var service = CreateService(discoveryMock, clientMock, stateStore);

        await service.PollAsync(CancellationToken.None);

        var state = stateStore.GetState("amass-engine-0")!;
        Assert.Equal(0, state.Sessions[0].ItemsPerSecond);
        Assert.NotNull(state.Sessions[0].LastPollTimestamp);
    }

    [Fact]
    public async Task PollAsync_SubsequentPoll_ComputesItemsPerSecond()
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
            .ReturnsAsync(new HealthCheckResponse { Result = "Amass Engine OK" });
        clientMock.Setup(c => c.ListSessionsAsync("10.0.0.1", 8080, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListSessionsResponse { SessionTokens = ["token-1"] });

        var stateStore = new EngineStateStore();
        var service = CreateService(discoveryMock, clientMock, stateStore);

        // First poll: 5 items completed
        clientMock.Setup(c => c.GetSessionStatsAsync("10.0.0.1", 8080, "token-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionStatsResponse { WorkItemsCompleted = 5, WorkItemsTotal = 100 });

        await service.PollAsync(CancellationToken.None);

        // Second poll: 15 items completed (delta = 10)
        clientMock.Setup(c => c.GetSessionStatsAsync("10.0.0.1", 8080, "token-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionStatsResponse { WorkItemsCompleted = 15, WorkItemsTotal = 100 });

        await service.PollAsync(CancellationToken.None);

        var state = stateStore.GetState("amass-engine-0")!;
        // Rate should be > 0 since delta is 10 and some time elapsed
        Assert.True(state.Sessions[0].ItemsPerSecond > 0);
        Assert.NotNull(state.Sessions[0].LastPollTimestamp);
    }

    [Fact]
    public async Task PollAsync_NoDelta_HasZeroItemsPerSecond()
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
            .ReturnsAsync(new HealthCheckResponse { Result = "Amass Engine OK" });
        clientMock.Setup(c => c.ListSessionsAsync("10.0.0.1", 8080, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListSessionsResponse { SessionTokens = ["token-1"] });
        clientMock.Setup(c => c.GetSessionStatsAsync("10.0.0.1", 8080, "token-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionStatsResponse { WorkItemsCompleted = 5, WorkItemsTotal = 100 });

        var stateStore = new EngineStateStore();
        var service = CreateService(discoveryMock, clientMock, stateStore);

        // Two polls with same completed count
        await service.PollAsync(CancellationToken.None);
        await service.PollAsync(CancellationToken.None);

        var state = stateStore.GetState("amass-engine-0")!;
        Assert.Equal(0, state.Sessions[0].ItemsPerSecond);
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
        var service = CreateService(discoveryMock, clientMock, stateStore);

        await service.PollAsync(CancellationToken.None);

        var state = stateStore.GetState("amass-engine-0");
        Assert.NotNull(state);
        Assert.False(state!.IsHealthy);
        Assert.Empty(state.Sessions);
    }

    [Fact]
    public async Task PollAsync_ParsesAnnotations_IntoAnnotationInfo()
    {
        var annotations = new Dictionary<string, string>
        {
            ["amass.io/public-ip"] = "1.2.3.4",
            ["amass.io/is-tor"] = "true",
            ["amass.io/ip-check-time"] = "2026-03-11T12:00:00Z",
            ["amass.io/ip-check-status"] = "ok"
        };

        var pods = new List<EnginePodInfo>
        {
            new("amass-engine-0", "10.0.0.1", 0, "Running", true, annotations)
        };

        var discoveryMock = new Mock<IKubernetesDiscoveryService>();
        discoveryMock.Setup(d => d.DiscoverEnginePodsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(pods);

        var clientMock = new Mock<IAmassEngineClient>();
        clientMock.Setup(c => c.HealthCheckAsync("10.0.0.1", 8080, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HealthCheckResponse { Result = "Amass Engine OK" });
        clientMock.Setup(c => c.ListSessionsAsync("10.0.0.1", 8080, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListSessionsResponse { SessionTokens = [] });

        var stateStore = new EngineStateStore();
        var service = CreateService(discoveryMock, clientMock, stateStore);

        await service.PollAsync(CancellationToken.None);

        var state = stateStore.GetState("amass-engine-0");
        Assert.NotNull(state?.AnnotationInfo);
        Assert.True(state!.AnnotationInfo!.IsTor);
        Assert.Equal("1.2.3.4", state.AnnotationInfo.PublicIP);
        Assert.Equal(IpCheckStatus.Ok, state.AnnotationInfo.CheckStatus);
        Assert.Null(state.AnnotationInfo.CheckError);
    }

    [Fact]
    public async Task PollAsync_HandlesAnnotation_ErrorStatus()
    {
        var annotations = new Dictionary<string, string>
        {
            ["amass.io/ip-check-status"] = "error:unreachable",
            ["amass.io/ip-check-time"] = "2026-03-11T12:00:00Z"
        };

        var pods = new List<EnginePodInfo>
        {
            new("amass-engine-0", "10.0.0.1", 0, "Running", true, annotations)
        };

        var discoveryMock = new Mock<IKubernetesDiscoveryService>();
        discoveryMock.Setup(d => d.DiscoverEnginePodsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(pods);

        var clientMock = new Mock<IAmassEngineClient>();
        clientMock.Setup(c => c.HealthCheckAsync("10.0.0.1", 8080, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HealthCheckResponse { Result = "Amass Engine OK" });
        clientMock.Setup(c => c.ListSessionsAsync("10.0.0.1", 8080, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListSessionsResponse { SessionTokens = [] });

        var stateStore = new EngineStateStore();
        var service = CreateService(discoveryMock, clientMock, stateStore);

        await service.PollAsync(CancellationToken.None);

        var state = stateStore.GetState("amass-engine-0");
        Assert.NotNull(state?.AnnotationInfo);
        Assert.Equal(IpCheckStatus.Error, state!.AnnotationInfo!.CheckStatus);
        Assert.Equal("error:unreachable", state.AnnotationInfo.CheckError);
    }

    [Fact]
    public async Task PollAsync_NoAnnotations_AnnotationInfoIsNull()
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
            .ReturnsAsync(new HealthCheckResponse { Result = "Amass Engine OK" });
        clientMock.Setup(c => c.ListSessionsAsync("10.0.0.1", 8080, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListSessionsResponse { SessionTokens = [] });

        var stateStore = new EngineStateStore();
        var service = CreateService(discoveryMock, clientMock, stateStore);

        await service.PollAsync(CancellationToken.None);

        var state = stateStore.GetState("amass-engine-0");
        Assert.NotNull(state);
        Assert.Null(state!.AnnotationInfo);
    }

    [Fact]
    public async Task PollAsync_PublicIpOnly_NoTorAnnotation_AnnotationInfoHasNullIsTor()
    {
        var annotations = new Dictionary<string, string>
        {
            ["amass.io/public-ip"] = "5.6.7.8",
            ["amass.io/ip-check-status"] = "ok",
            ["amass.io/ip-check-time"] = "2026-03-11T12:00:00Z"
        };

        var pods = new List<EnginePodInfo>
        {
            new("amass-engine-0", "10.0.0.1", 0, "Running", true, annotations)
        };

        var discoveryMock = new Mock<IKubernetesDiscoveryService>();
        discoveryMock.Setup(d => d.DiscoverEnginePodsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(pods);

        var clientMock = new Mock<IAmassEngineClient>();
        clientMock.Setup(c => c.HealthCheckAsync("10.0.0.1", 8080, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HealthCheckResponse { Result = "Amass Engine OK" });
        clientMock.Setup(c => c.ListSessionsAsync("10.0.0.1", 8080, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListSessionsResponse { SessionTokens = [] });

        var stateStore = new EngineStateStore();
        var service = CreateService(discoveryMock, clientMock, stateStore);

        await service.PollAsync(CancellationToken.None);

        var state = stateStore.GetState("amass-engine-0");
        Assert.NotNull(state?.AnnotationInfo);
        Assert.Equal("5.6.7.8", state!.AnnotationInfo!.PublicIP);
        Assert.Null(state.AnnotationInfo.IsTor);
        Assert.Equal(IpCheckStatus.Ok, state.AnnotationInfo.CheckStatus);
    }
}
