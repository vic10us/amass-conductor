---
marp: true
theme: default
paginate: true
---

# Amass Conductor

### Operations Deep Dive

---

## Session Lifecycle

```
              ┌──────────┐
              │  Active   │
              └─────┬─────┘
         ┌──────────┼──────────┐
         ▼          ▼          ▼
   ┌──────────┐ ┌────────┐ ┌────────┐
   │Completed │ │Cancelled│ │ Failed │
   └────┬─────┘ └────┬───┘ └────┬───┘
        └─────────────┼─────────┘
                      ▼
              Retry / Restart / Delete
```

- **Completed** — all work items processed (confirmed by poll threshold)
- **Cancelled** — user-initiated stop
- **Failed** — engine error or orphaned session (pod disappeared)

---

## Engine Discovery & Health Monitoring

- Pods discovered via Kubernetes API with label selector:
  `app.kubernetes.io/name=amass, app.kubernetes.io/component=engine`
- Health checked every `PollIntervalSeconds` (default: 2s)
- Each poll: `GET /health` + `GET /sessions/list` + `GET /sessions/{token}/stats`
- Pods processed in parallel (`Parallel.ForEachAsync`)
- Stale pods removed from state store when no longer discovered

---

## Session Creation Flow

Engine selection algorithm:

1. Filter engines: **healthy** and **ready**
2. Filter engines: `ActiveSessionCount < MaxActiveSessionsPerEngine`
3. Sort by: fewest active sessions → fewest total sessions → lowest ordinal
4. Select first (best) engine

Then:
- `POST /sessions` → receive session token
- `POST /sessions/{token}/assets/fqdn:bulk` → submit domains
- Persist session record with full config to SQLite

---

## Real-Time Monitoring

- **Throughput tracking**: items/s calculated from work-item delta between polls
- **Progress bars**: `WorkItemsCompleted / WorkItemsTotal` per session
- **Aggregated view**: engine cards show combined progress across sessions
- **Live updates**: `EngineStateStore.OnStateChanged` → Blazor re-renders

---

## Completion Detection

A session is **not** marked complete the first time `completed == total`.

Instead: **CompletionPollThreshold** (default: 5) consecutive polls must all show `completed == total`.

**Why?** Work items may temporarily show 100% before all items are queued. The threshold prevents false completion.

```
Poll 1: 50/50  → consecutivePolls = 1
Poll 2: 50/55  → consecutivePolls = 0  (new items queued)
Poll 3: 55/55  → consecutivePolls = 1
Poll 4: 55/55  → consecutivePolls = 2
...
Poll 7: 55/55  → consecutivePolls = 5  ✓ COMPLETED
```

---

## Failure Handling

**Engine errors:**
- Health check returns non-200 → engine marked unhealthy
- Session stats request fails → session marked failed with error message

**Retry flow:**
- Session config stored as JSON in SQLite at creation time
- Retry re-opens CreateSessionDialog pre-populated with saved config
- User can choose: same engine or auto-select best available

---

## Orphan Detection

Sessions can become orphaned when engine pods restart or are deleted.

**Detection logic** (runs after each poll cycle):
1. Query database for all active sessions
2. Compare against live sessions reported by engines
3. Sessions in DB but not on any engine → marked as **Failed**
4. Error message: indicates the session was lost due to pod lifecycle

---

## Tor Integration

- Engine pods can run a Tor sidecar for anonymous enumeration
- Sidecar writes results to pod annotations:
  - Public IP address
  - Connection type (Tor / Direct)
  - Check status (Success / Error)
  - Last checked timestamp
- `KubernetesDiscoveryService` reads annotations via `PodAnnotationInfo`
- UI shows Tor/Direct badges on engine cards and detail pages

---

## Bulk Operations

From the Sessions list page:

- **Multi-select** sessions with checkboxes
- **Stop Selected** — cancel all selected active sessions
- **Delete Selected** — remove completed/failed/cancelled sessions from DB
- **Restart Selected** — re-create sessions with saved configurations

Individual session actions available inline and on detail page.

---

## Log Aggregation

- `LogAggregatorService` maintains WebSocket connections to all active sessions
- Each log line persisted to SQLite (`LogRecord` table)
- `OnLogReceived` event broadcasts to live subscribers
- **LiveLogs page**: combines historical (DB) + live (WebSocket) logs
- Filters: engine, session, text search
- Last 1,000 live entries kept in memory

---

## Configuration & Tuning

| Setting | Default | Operational Impact |
|---------|---------|-------------------|
| `PollIntervalSeconds` | 2 | Lower = faster updates, more API calls |
| `MaxActiveSessionsPerEngine` | 1 | Higher = more concurrency per pod |
| `CompletionPollThreshold` | 5 | Higher = fewer false completions |
| `AutoDeleteCompletedSessions` | true | Cleans up engine resources automatically |
| `HttpClientTimeoutSeconds` | 10 | Timeout for engine REST calls |
| `WebSocketBufferSize` | 4096 | Buffer for log stream reads |

Settings page allows runtime changes (not persisted to file).

---

## Operational Dashboard

**Statistics page** provides:

- Total / Completed / Failed / Active session counts
- Success rate percentage
- Average, min, max session duration
- Average throughput (items/s)
- Sessions per day over last 30 days
- Duration and throughput per session (last 20)
- Work items per session breakdown
- Sessions per engine distribution

---

## Questions?

**Docs:** `docs/ARCHITECTURE.md` · `docs/USAGE_GUIDE.md` · `docs/ENGINE_API_GUIDE.md`
