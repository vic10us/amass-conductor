using AmassOrchestrator.Web.Models.Kubernetes;
using AmassOrchestrator.Web.Services;

namespace AmassOrchestrator.Tests.Services;

public class EngineStateStoreTests
{
    private static EngineInstanceState MakeState(string podName, bool healthy = true) =>
        new(new EnginePodInfo(podName, "10.0.0.1", 0, "Running", true), healthy, []);

    [Fact]
    public void UpdateState_StoresAndRetrieves()
    {
        var store = new EngineStateStore();
        var state = MakeState("pod-0");

        store.UpdateState("pod-0", state);

        Assert.NotNull(store.GetState("pod-0"));
        Assert.Equal("pod-0", store.GetState("pod-0")!.Pod.PodName);
    }

    [Fact]
    public void GetState_ReturnsNull_WhenNotFound()
    {
        var store = new EngineStateStore();
        Assert.Null(store.GetState("nonexistent"));
    }

    [Fact]
    public void RemoveStale_RemovesOldPods()
    {
        var store = new EngineStateStore();
        store.UpdateState("pod-0", MakeState("pod-0"));
        store.UpdateState("pod-1", MakeState("pod-1"));

        store.RemoveStale(new HashSet<string> { "pod-0" });

        Assert.NotNull(store.GetState("pod-0"));
        Assert.Null(store.GetState("pod-1"));
    }

    [Fact]
    public void OnStateChanged_FiresOnUpdate()
    {
        var store = new EngineStateStore();
        var fired = false;
        store.OnStateChanged += () => fired = true;

        store.UpdateState("pod-0", MakeState("pod-0"));

        Assert.True(fired);
    }

    [Fact]
    public void OnStateChanged_FiresOnRemoveStale()
    {
        var store = new EngineStateStore();
        store.UpdateState("pod-0", MakeState("pod-0"));

        var fired = false;
        store.OnStateChanged += () => fired = true;

        store.RemoveStale(new HashSet<string>());

        Assert.True(fired);
    }

    [Fact]
    public void States_ReturnsAllEntries()
    {
        var store = new EngineStateStore();
        store.UpdateState("pod-0", MakeState("pod-0"));
        store.UpdateState("pod-1", MakeState("pod-1"));

        Assert.Equal(2, store.States.Count);
    }
}
