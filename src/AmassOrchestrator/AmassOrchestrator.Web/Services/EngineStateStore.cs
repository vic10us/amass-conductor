using System.Collections.Concurrent;
using AmassOrchestrator.Web.Models.Kubernetes;

namespace AmassOrchestrator.Web.Services;

public class EngineStateStore
{
    private readonly ConcurrentDictionary<string, EngineInstanceState> _states = new();

    public event Action? OnStateChanged;

    public IReadOnlyDictionary<string, EngineInstanceState> States => _states;

    public EngineInstanceState? GetState(string podName) =>
        _states.TryGetValue(podName, out var state) ? state : null;

    public void UpdateState(string podName, EngineInstanceState state)
    {
        _states[podName] = state;
        NotifyStateChanged();
    }

    public void RemoveStale(ISet<string> activePodNames)
    {
        var staleKeys = _states.Keys.Where(k => !activePodNames.Contains(k)).ToList();
        var removed = false;
        foreach (var key in staleKeys)
        {
            _states.TryRemove(key, out _);
            removed = true;
        }
        if (removed) NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnStateChanged?.Invoke();
}
