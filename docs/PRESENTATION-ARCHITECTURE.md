---
marp: true
theme: default
paginate: true
---

# Amass Conductor

### Architecture Deep Dive

---

## System Architecture

```
┌────────────────────────────────────────────────────────┐
│                      Browser                            │
│  Dashboard · Sessions · Logs · Statistics · Settings    │
└──────────────┬──────────────────────────────────────────┘
               │ SignalR (Blazor Server)
┌──────────────▼──────────────────────────────────────────┐
│              Blazor Server (.NET 10)                     │
│                                                          │
│  ┌────────────────┐ ┌───────────────┐ ┌───────────────┐ │
│  │EngineMonitor   │ │LogAggregator  │ │Enumeration    │ │
│  │Service         │ │Service        │ │Service        │ │
│  │(Background)    │ │(Background)   │ │               │ │
│  └───────┬────────┘ └──────┬────────┘ └──────┬────────┘ │
│          └─────────────────┼─────────────────┘          │
│                    ┌───────▼────────┐                    │
│                    │EngineStateStore│                    │
│                    │(ConcurrentDict)│                    │
│                    └───────┬────────┘                    │
│          ┌─────────────────┼─────────────────┐          │
│  ┌───────▼────────┐ ┌─────▼──────┐ ┌────────▼───────┐  │
│  │K8sDiscovery    │ │AmassEngine │ │Session         │  │
│  │Service         │ │Client      │ │Repository      │  │
│  └───────┬────────┘ └─────┬──────┘ └────────┬───────┘  │
└──────────┼─────────────────┼─────────────────┼──────────┘
   ┌───────▼───────┐ ┌──────▼──────┐ ┌────────▼───────┐
   │K8s API Server │ │Engine Pods  │ │SQLite DB       │
   └───────────────┘ │REST + WS   │ │orchestrator.db │
                     └─────────────┘ └────────────────┘
```

---

## Technology Choices & Rationale

| Choice | Why |
|--------|-----|
| **.NET 10** | Latest runtime, strong async/concurrency, K8s client support |
| **Blazor Server** | Real-time UI without JS framework, SignalR push updates |
| **Radzen.Blazor** | Rich component library (grids, charts, dialogs) with minimal code |
| **EF Core + SQLite** | Zero-infrastructure persistence, portable, sufficient for single-instance |
| **KubernetesClient** | Official C# client, typed API, watches and label selectors |
| **Serilog** | Structured logging, file + console sinks, JSON format |
| **Polly** | HTTP resilience via `Microsoft.Extensions.Http.Resilience` |

---

## Background Services Pattern

Both services inherit from `BackgroundService` (`IHostedService`):

**EngineMonitorService:**
- Timer-based polling loop (`PollIntervalSeconds`)
- Discovers pods → health checks → session stats → state update
- Runs for application lifetime

**LogAggregatorService:**
- Event-driven + timeout hybrid
- Subscribes to `EngineStateStore.OnStateChanged`
- Wakes on state change OR every 30 seconds
- Syncs WebSocket streams to match active sessions

Registered in DI as `AddHostedService<T>()`.

---

## EngineMonitorService Internals

```
ExecuteAsync (loop every PollIntervalSeconds)
    │
    ├─ DiscoverEnginePodsAsync()         ← K8s API
    │
    ├─ Parallel.ForEachAsync(pods)       ← concurrent pod processing
    │   ├─ HealthCheckAsync()            ← GET /health
    │   ├─ ListSessionsAsync()           ← GET /sessions/list
    │   ├─ GetSessionStatsAsync()        ← GET /sessions/{token}/stats
    │   ├─ Calculate throughput (Δ items / Δ time)
    │   └─ EngineStateStore.UpdateState()
    │
    ├─ RemoveStale(activePodNames)       ← clean up disappeared pods
    │
    └─ DetectOrphanedSessionsAsync()     ← mark lost sessions as failed
```

---

## LogAggregatorService Internals

```
ExecuteAsync (event-driven loop)
    │
    ├─ Wait for signal (OnStateChanged OR 30s timeout)
    │
    └─ SyncStreams()
        ├─ Collect active sessions from EngineStateStore
        ├─ Start new streams (fire-and-forget tasks)
        │   └─ StreamSessionLogs()
        │       ├─ AmassEngineClient.StreamLogsAsync()  ← WebSocket
        │       ├─ SessionRepository.AddLogAsync()      ← persist
        │       └─ OnLogReceived.Invoke()               ← broadcast
        └─ Stop streams for completed sessions
```

**Stream tracking:** `Dictionary<string, CancellationTokenSource>` keyed by session token.

---

## State Management

`EngineStateStore` is the single source of truth for live engine state.

```csharp
ConcurrentDictionary<string, EngineInstanceState>
```

- **Writers:** EngineMonitorService (poll results)
- **Readers:** Blazor pages, EnumerationService, LogAggregatorService
- **Notification:** `OnStateChanged` event → subscribers call `StateHasChanged()`

No persistence needed — state is rebuilt from live data every poll cycle. Historical data lives in SQLite via SessionRepository.

---

## Engine Communication

`AmassEngineClient` wraps HTTP and WebSocket calls:

| Method | Verb | Endpoint | Returns |
|--------|------|----------|---------|
| HealthCheck | GET | `/health` | bool |
| ListSessions | GET | `/sessions/list` | SessionInfo[] |
| CreateSession | POST | `/sessions` | CreateSessionResponse |
| DeleteSession | DELETE | `/sessions/{token}` | bool |
| GetSessionStats | GET | `/sessions/{token}/stats` | EnumerationResult |
| GetScope | GET | `/sessions/{token}/scope/{type}` | object[] |
| AddAsset | POST | `/sessions/{token}/assets/{type}` | bool |
| BulkAddAssets | POST | `/sessions/{token}/assets/{type}:bulk` | bool |
| StreamLogs | WS | `/sessions/{token}/ws/logs` | IAsyncEnumerable |

Uses `IHttpClientFactory` with named client "AmassEngine".

---

## Kubernetes Integration

`KubernetesDiscoveryService` provides pod discovery:

- Queries `ListNamespacedPodAsync(namespace, labelSelector)`
- Maps `V1Pod` → `EnginePodInfo`:
  - Name, IP, Phase, Ready status
  - Ordinal (parsed from StatefulSet pod name suffix)
  - Annotations → `PodAnnotationInfo` (Tor status, public IP)
- Returns sorted by ordinal for deterministic ordering
- Graceful degradation: returns empty list on API failure

---

## Data Layer

**EF Core + SQLite** with two entities:

**SessionRecord** — enumeration metadata:
- Token, EnginePodName, WorkItems (completed/total)
- Status flags: IsCompleted, IsFailed, IsCancelled
- ConfigJson: full Amass config serialized at creation time
- Timestamps: Created, Updated, Completed

**LogRecord** — captured log messages:
- SessionToken, EnginePodName, Message, TimestampUtc

`SessionRepository` provides typed queries: active sessions, filtered logs, config retrieval, status mutations.

---

## Session Data Flow

```
1. User clicks "New Enumeration"
2. CreateSessionDialog collects config (6 tabs)
3. EnumerationService selects engine (healthy, under capacity, lowest load)
4. POST /sessions → engine creates Amass process, returns token
5. POST /sessions/{token}/assets/fqdn:bulk → submit target domains
6. SessionRepository.CreateAsync() → persist to SQLite with config JSON
7. EngineMonitorService picks up session on next poll → updates state
8. LogAggregatorService detects active session → opens WebSocket stream
```

---

## Configuration Architecture

`OrchestratorOptions` binds to `appsettings.json` section `"Orchestrator"`:

```csharp
services.Configure<OrchestratorOptions>(
    config.GetSection("Orchestrator"));
```

- Injected as `IOptionsMonitor<OrchestratorOptions>` for live reload
- Settings page updates `CurrentValue` properties at runtime
- Environment variable override: `Orchestrator__Key=value`
- 15 configuration keys controlling namespace, polling, capacity, paths

---

## Blazor Component Architecture

```
Layout/
├── MainLayout.razor        Radzen shell: header, sidebar, body
└── NavMenu.razor            Navigation items

Pages/
├── Dashboard.razor          Engine card grid, "New Enumeration" button
├── Sessions.razor           Data grid, filters, bulk ops
├── SessionDetail.razor      4-tab detail (Stats, Logs, Scope, Config)
├── EngineDetail.razor       Pod info, network card, session list
├── Statistics.razor         Summary cards + 6 Radzen charts
├── LiveLogs.razor           Aggregated log viewer with live toggle
├── Settings.razor           Runtime config form
└── CreateSessionDialog.razor 6-tab creation form (modal)

Shared/
├── EngineCard.razor         Pod status card component
├── ProgressBar.razor        Work item progress with throughput
├── HealthBadge.razor        Health status indicator
├── SessionControls.razor    Stop button with confirmation
├── LogViewer.razor          WebSocket log streamer
└── ScopeTable.razor         Paginated asset type table
```

---

## Testing Strategy

**Framework:** xUnit + Moq + bunit

**Service tests** (`AmassOrchestrator.Tests/Services/`):
- `EngineMonitorServiceTests` — poll loop, orphan detection, completion threshold
- `EnumerationServiceTests` — engine selection, session creation, asset submission
- `AmassEngineClientTests` — HTTP mocking, WebSocket streaming, error handling
- `KubernetesDiscoveryServiceTests` — K8s API mocking, pod mapping
- `EngineStateStoreTests` — thread safety, event firing, stale cleanup

**Model tests** (`AmassOrchestrator.Tests/Models/`):
- `PodAnnotationInfoTests` — annotation parsing

All services use interface-based DI for testability.

---

## Questions?

**Docs:** `docs/ARCHITECTURE.md` · `docs/USAGE_GUIDE.md` · `docs/ENGINE_API_GUIDE.md`
