using AmassOrchestrator.Web.Configuration;
using AmassOrchestrator.Web.Services;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace AmassOrchestrator.Tests.Services;

public class KubernetesDiscoveryServiceTests
{
    private static IOptions<OrchestratorOptions> DefaultOptions() =>
        Options.Create(new OrchestratorOptions
        {
            Namespace = "test-ns",
            StatefulSetName = "amass-engine",
            EnginePort = 8080
        });

    private static Mock<IKubernetes> SetupK8sMock(V1PodList podList)
    {
        var k8sMock = new Mock<IKubernetes>();
        var coreV1Mock = new Mock<ICoreV1Operations>();

        // KubernetesClient v19 signature:
        // (string ns, bool? allowWatchBookmarks, string continue, string fieldSelector,
        //  string labelSelector, int? limit, string resourceVersion, string resourceVersionMatch,
        //  bool? sendInitialEvents, int? timeoutSeconds, bool? watch, bool? pretty,
        //  IReadOnlyDictionary<string,IReadOnlyList<string>> customHeaders, CancellationToken ct)
        coreV1Mock
            .Setup(c => c.ListNamespacedPodWithHttpMessagesAsync(
                It.IsAny<string>(),     // namespace
                It.IsAny<bool?>(),      // allowWatchBookmarks
                It.IsAny<string>(),     // continue
                It.IsAny<string>(),     // fieldSelector
                It.IsAny<string>(),     // labelSelector
                It.IsAny<int?>(),       // limit
                It.IsAny<string>(),     // resourceVersion
                It.IsAny<string>(),     // resourceVersionMatch
                It.IsAny<bool?>(),      // sendInitialEvents
                It.IsAny<int?>(),       // timeoutSeconds
                It.IsAny<bool?>(),      // watch
                It.IsAny<bool?>(),      // pretty
                It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<string>>>(), // customHeaders
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new k8s.Autorest.HttpOperationResponse<V1PodList> { Body = podList });

        k8sMock.Setup(k => k.CoreV1).Returns(coreV1Mock.Object);
        return k8sMock;
    }

    [Fact]
    public async Task DiscoverEnginePodsAsync_ReturnsPods_OrderedByOrdinal()
    {
        var podList = new V1PodList
        {
            Items =
            [
                MakePod("amass-engine-1", "10.0.0.2", "Running", true),
                MakePod("amass-engine-0", "10.0.0.1", "Running", true),
            ]
        };

        var k8sMock = SetupK8sMock(podList);

        var service = new KubernetesDiscoveryService(
            k8sMock.Object, DefaultOptions(), NullLogger<KubernetesDiscoveryService>.Instance);

        var result = await service.DiscoverEnginePodsAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("amass-engine-0", result[0].PodName);
        Assert.Equal("amass-engine-1", result[1].PodName);
        Assert.Equal(0, result[0].Ordinal);
        Assert.Equal(1, result[1].Ordinal);
    }

    [Fact]
    public async Task DiscoverEnginePodsAsync_SkipsPods_WithoutIP()
    {
        var podList = new V1PodList
        {
            Items =
            [
                MakePod("amass-engine-0", null!, "Pending", false),
                MakePod("amass-engine-1", "10.0.0.2", "Running", true),
            ]
        };

        var k8sMock = SetupK8sMock(podList);

        var service = new KubernetesDiscoveryService(
            k8sMock.Object, DefaultOptions(), NullLogger<KubernetesDiscoveryService>.Instance);

        var result = await service.DiscoverEnginePodsAsync();

        Assert.Single(result);
        Assert.Equal("amass-engine-1", result[0].PodName);
    }

    [Fact]
    public async Task DiscoverEnginePodsAsync_ReturnsEmpty_OnException()
    {
        var k8sMock = new Mock<IKubernetes>();
        var coreV1Mock = new Mock<ICoreV1Operations>();

        coreV1Mock
            .Setup(c => c.ListNamespacedPodWithHttpMessagesAsync(
                It.IsAny<string>(),
                It.IsAny<bool?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool?>(),
                It.IsAny<int?>(),
                It.IsAny<bool?>(),
                It.IsAny<bool?>(),
                It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<string>>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("K8s connection failed"));

        k8sMock.Setup(k => k.CoreV1).Returns(coreV1Mock.Object);

        var service = new KubernetesDiscoveryService(
            k8sMock.Object, DefaultOptions(), NullLogger<KubernetesDiscoveryService>.Instance);

        var result = await service.DiscoverEnginePodsAsync();

        Assert.Empty(result);
    }

    private static V1Pod MakePod(string name, string ip, string phase, bool ready)
    {
        return new V1Pod
        {
            Metadata = new V1ObjectMeta { Name = name },
            Status = new V1PodStatus
            {
                PodIP = ip,
                Phase = phase,
                Conditions = ready
                    ? [new V1PodCondition { Type = "Ready", Status = "True" }]
                    : [new V1PodCondition { Type = "Ready", Status = "False" }]
            }
        };
    }
}
