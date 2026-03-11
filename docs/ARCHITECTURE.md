# Architecture

Technical deep-dive into the Amass Conductor system design.

## System Overview

```
┌──────────────────────────────────────────────────────────────────────┐
│                          Browser                                     │
│  Dashboard · Sessions · Logs · Statistics · Settings                 │
└──────────────────┬───────────────────────────────────────────────────┘
                   │ SignalR (Blazor Server)
┌──────────────────▼───────────────────────────────────────────────────┐
│                      Blazor Server (.NET 10)                         │
│                                                                      │
│  ┌─────────────────┐  ┌──────────────────┐  ┌────────────────────┐  │
│  │ EngineMonitor    │  │ LogAggregator    │  │ EnumerationService │  │
│  │ Service          │  │ Service          │  │                    │  │
│  │ (BackgroundSvc)  │  │ (BackgroundSvc)  │  │                    │  │
│  └───────┬──────────┘  └───────┬──────────┘  └─────────┬──────────┘  │
│          │                     │                       │              │
│  ┌───────▼──────────────────────────────────────────────▼──────────┐  │
│  │                    EngineStateStore                              │  │
│  │              (ConcurrentDictionary + Events)                    │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│          │                     │                       │              │
│  ┌───────▼──────────┐  ┌──────▼───────────┐  ┌────────▼───────────┐  │
│  │ K8sDiscovery     │  │ AmassEngine      │  │ SessionRepository  │  │
│  │ Service          │  │ Client           │  │ (EF Core/SQLite)   │  │
│  └───────┬──────────┘  └──────┬───────────┘  └────────┬───────────┘  │
└──────────┼─────────────────────┼───────────────────────┼─────────────┘
           │                     │                       │
   ┌───────▼──────┐    ┌────────▼────────┐     ┌────────▼────────┐
   │ Kubernetes    │    │ Engine Pods     │     │ SQLite DB       │
   │ API Server    │    │ REST + WS      │     │ orchestrator.db │
   └──────────────┘    └─────────────────┘     └─────────────────┘
```

## Background Services

### EngineMonitorService

`Services/EngineMonitorService.cs` — `BackgroundService` that runs for the application lifetime.

**Poll loop:**
1. Call `KubernetesDiscoveryService.DiscoverEnginePodsAsync()` to list pods
2. For each pod (in parallel via `Parallel.ForEachAsync`):
   - `AmassEngineClient.HealthCheckAsync()` — check if healthy
   - `AmassEngineClient.ListSessionsAsync()` — get active sessions
   - `AmassEngineClient.GetSessionStatsAsync()` — get work item counts per session
3. Calculate throughput (items/s) from work-item delta since previous poll
4. Update `EngineStateStore` with new state → triggers Blazor re-renders
5. Remove stale pods no longer returned by discovery
6. Run orphan detection (see below)

**Completion detection:**
A session is marked completed only after `CompletionPollThreshold` consecutive polls where `WorkItemsCompleted == WorkItemsTotal`. This prevents false positives from brief moments where the counters align before all work is queued.

**Orphan detection:**
After polling, the service checks the database for active sessions whose engine pod no longer exists (pod restarted or deleted). These orphaned sessions are automatically marked as failed with an appropriate error message.

**Auto-deletion:**
When `AutoDeleteCompletedSessions` is enabled, completed sessions are deleted from the engine via the REST API after being marked complete. The session record persists in the local database.

### LogAggregatorService

`Services/LogAggregatorService.cs` — `BackgroundService` that manages WebSocket log streams.

**Event-driven activation:**
- Subscribes to `EngineStateStore.OnStateChanged`
- Wakes up on state change OR every 30 seconds (timeout fallback)
- Uses `TaskCompletionSource` as a signal mechanism

**Stream synchronization:**
1. Collect all active sessions from the state store
2. Compare with currently streaming sessions
3. Start new WebSocket streams for new sessions (fire-and-forget tasks)
4. Stop streams for sessions that are no longer active

**Log handling:**
- Each stream calls `AmassEngineClient.StreamLogsAsync()` (WebSocket `IAsyncEnumerable`)
- Received log lines are persisted to the database via `SessionRepository.AddLogAsync()`
- `OnLogReceived` event broadcasts to subscribers (used by LiveLogs page)

## Core Services

### EnumerationService

`Services/EnumerationService.cs` — orchestrates starting new enumerations.

**Engine selection algorithm** (`StartEnumerationAsync`):
1. Get all engine states from `EngineStateStore`
2. Filter to healthy engines with `ActiveSessionCount < MaxActiveSessionsPerEngine`
3. Sort by: fewest active sessions → fewest total sessions → lowest ordinal
4. Select the first (best) engine

**Session creation flow** (`RunEnumerationAsync`):
1. `AmassEngineClient.CreateSessionAsync()` — POST config to engine, receive session token
2. `AmassEngineClient.BulkAddAssetsAsync()` — submit domains as FQDN assets
3. `SessionRepository.CreateAsync()` — persist session record with serialized config JSON

Also supports `StartEnumerationOnEngineAsync()` for targeting a specific engine pod.

### AmassEngineClient

`Services/AmassEngineClient.cs` — HTTP and WebSocket wrapper for the engine REST API.

**REST methods:**
| Method | Endpoint | Description |
|--------|----------|-------------|
| `HealthCheckAsync` | `GET /health` | Returns true if 200 |
| `ListSessionsAsync` | `GET /sessions/list` | Returns session list (404 → empty) |
| `CreateSessionAsync` | `POST /sessions` | Creates session, returns token |
| `DeleteSessionAsync` | `DELETE /sessions/{token}` | Deletes session |
| `GetSessionStatsAsync` | `GET /sessions/{token}/stats` | Work item counts |
| `GetScopeAsync` | `GET /sessions/{token}/scope/{type}` | Scope assets by type |
| `AddAssetAsync` | `POST /sessions/{token}/assets/{type}` | Add single asset |
| `BulkAddAssetsAsync` | `POST /sessions/{token}/assets/{type}:bulk` | Add assets in bulk |

**WebSocket method:**
| Method | Endpoint | Description |
|--------|----------|-------------|
| `StreamLogsAsync` | `WS /sessions/{token}/ws/logs` | Returns `IAsyncEnumerable<string>` |

See [Engine API Guide](ENGINE_API_GUIDE.md) for full API documentation.

### KubernetesDiscoveryService

`Services/KubernetesDiscoveryService.cs` — discovers engine pods via the Kubernetes API.

- Uses the official `KubernetesClient` library
- Queries pods by namespace and label selector
- Extracts: pod name, IP, phase, readiness, ordinal (parsed from pod name suffix)
- Reads pod annotations for Tor status (via `PodAnnotationInfo`)
- Returns pods sorted by ordinal for consistent ordering
- Returns empty list on failure (graceful degradation)

### SessionRepository

`Services/SessionRepository.cs` — EF Core data access layer for SQLite.

Manages `SessionRecord` and `LogRecord` entities. Key operations:
- CRUD for sessions (create, upsert, get by token, delete)
- Status mutations: `MarkCancelled`, `MarkFailed`, `CreateFailed`
- Log operations: `AddLogAsync`, `GetLogsAsync`, `GetFilteredLogsAsync`
- Queries: active sessions, all sessions, by token, config JSON retrieval

## State Management

### EngineStateStore

`Services/EngineStateStore.cs` — central in-memory state for all engine pods.

```
ConcurrentDictionary<string, EngineInstanceState>
    key = pod name
    value = { PodInfo, IsHealthy, Sessions[], ThroughputMetrics }
```

- **Thread-safe**: concurrent reads/writes from monitor service, UI components, enumeration service
- **OnStateChanged event**: fires on any `UpdateState()` or `RemoveStale()` call
- Blazor components subscribe to this event and call `InvokeAsync(StateHasChanged)` to re-render
- No persistence — rebuilt from live data on every poll cycle

## Data Flows

### Creating an Enumeration

```
User clicks "New Enumeration"
    │
    ▼
CreateSessionDialog opens (6-tab form)
    │ User fills in domains, options, wordlists, etc.
    ▼
EnumerationService.StartEnumerationAsync()
    │
    ├─ 1. Query EngineStateStore for healthy engines
    ├─ 2. Select least-loaded engine under capacity
    ├─ 3. POST /sessions to engine → receive token
    ├─ 4. POST /sessions/{token}/assets/fqdn:bulk → submit domains
    ├─ 5. SessionRepository.CreateAsync() → persist to SQLite
    │
    ▼
EngineMonitorService picks up new session on next poll
    │
    ├─ 6. GET /sessions/{token}/stats → work item counts
    ├─ 7. Update EngineStateStore → triggers Blazor re-render
    │
    ▼
LogAggregatorService detects new active session
    │
    └─ 8. Opens WebSocket → streams logs → persists + broadcasts
```

### Session Lifecycle State Machine

```
                    ┌─────────────┐
                    │   Active    │
                    └──────┬──────┘
                           │
              ┌────────────┼────────────┐
              │            │            │
              ▼            ▼            ▼
        ┌──────────┐ ┌──────────┐ ┌──────────┐
        │Completed │ │Cancelled │ │  Failed  │
        └─────┬────┘ └─────┬────┘ └─────┬────┘
              │            │            │
              └────────────┼────────────┘
                           │
                    ┌──────▼──────┐
                    │ Retry /     │
                    │ Restart /   │
                    │ Delete      │
                    └─────────────┘
```

- **Active → Completed**: `CompletionPollThreshold` consecutive polls with all work items done
- **Active → Cancelled**: user clicks Stop
- **Active → Failed**: engine error or pod disappears (orphan detection)
- **Completed/Cancelled/Failed → Retry**: re-creates session with saved config on same or different engine
- **Any terminal → Delete**: removes session record and associated logs from database

## Database Schema

### SessionRecord

Stores enumeration session metadata and configuration.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | int (PK) | Auto-increment primary key |
| `Token` | string (unique) | Session UUID from engine |
| `EnginePodName` | string | Pod that ran this session |
| `WorkItemsCompleted` | int | Items processed |
| `WorkItemsTotal` | int | Total items |
| `ConsecutiveCompletionPolls` | int | Polls at 100% completion |
| `IsCompleted` | bool | Terminal: success |
| `IsFailed` | bool | Terminal: failure |
| `IsCancelled` | bool | Terminal: user cancelled |
| `ErrorMessage` | string? | Failure reason |
| `Domains` | string? | JSON array of domain strings |
| `ConfigJson` | string? | Full serialized AmassConfig |
| `CreatedAtUtc` | DateTime | Session start |
| `UpdatedAtUtc` | DateTime | Last status update |
| `CompletedAtUtc` | DateTime? | Session end |

### LogRecord

Stores log messages captured from engine WebSocket streams.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | long (PK) | Auto-increment primary key |
| `SessionToken` | string | Foreign key to session |
| `EnginePodName` | string | Source engine pod |
| `Message` | string | Log message text |
| `TimestampUtc` | DateTime | When the message was received |
