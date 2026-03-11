---
marp: true
theme: default
paginate: true
---

# Amass Conductor

### Orchestrating Domain Enumeration at Scale

---

## The Problem

- Amass CLI runs on a single machine — no visibility across teams
- Manual session management: start, monitor, collect results by hand
- No retry on failure — lost progress requires full restart
- No centralized log aggregation or historical statistics
- Scaling means SSH-ing into more boxes

---

## The Solution

- **One-click enumeration** across a fleet of Kubernetes engine pods
- **Real-time monitoring** — progress bars, throughput, WebSocket logs
- **Session lifecycle management** — stop, retry, restart, delete
- **Persistent history** — every session and log line stored in SQLite
- **Statistics dashboard** — success rates, duration trends, throughput charts

---

## Architecture

```
         Browser
           │  SignalR
    ┌──────▼──────┐
    │ Blazor      │──── Kubernetes API (pod discovery)
    │ Server      │──── Engine REST API (sessions, assets)
    │ (.NET 10)   │──── Engine WebSocket (log streaming)
    │             │──── SQLite (persistence)
    └─────────────┘
           │
    ┌──────▼──────────────────────────┐
    │  Engine Pod 0  │  Engine Pod 1  │ ...
    │  (Amass)       │  (Amass)       │
    └─────────────────────────────────┘
```

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 10.0 |
| UI | Blazor Server + Radzen.Blazor |
| Database | SQLite via EF Core 10 |
| Kubernetes | KubernetesClient |
| Logging | Serilog |
| Resilience | Polly (via Http.Resilience) |

---

## Demo: Dashboard

- Each engine pod displayed as a status card
- Health badge: Healthy / Unhealthy / Not Ready
- Tor/Direct connection indicator
- Active session count with aggregated progress
- Click card → engine detail

---

## Demo: Creating a Session

Six-tab configuration dialog:

1. **Scope** — domains to enumerate
2. **Options** — active probing, brute force, alterations
3. **Wordlists** — customizable name lists
4. **Database** — optional external DB connection
5. **Datasources** — API keys (YAML)
6. **Transformations** — enable/disable with priority and confidence

---

## Demo: Session Monitoring

- **Stats tab** — progress bar, items/s throughput, completion %
- **Logs tab** — real-time WebSocket log stream
- **Scope tab** — discovered assets by type (FQDN, IP, Netblock, ...)
- **Config tab** — full configuration snapshot

---

## Demo: Sessions List

- Filter by status, engine, domain text search
- Bulk operations: stop, delete, restart selected sessions
- Color-coded status badges
- Relative timestamps
- Click-through to session detail

---

## Demo: Logs & Statistics

**Logs page:**
- Aggregated view across all engines and sessions
- Filter by engine, session, text search
- Toggle live streaming on/off

**Statistics page:**
- Summary cards (total, completed, failed, success rate)
- Charts: sessions/day, success vs failure, duration, throughput

---

## Key Features Summary

- Auto-discovery of engine pods via Kubernetes labels
- Intelligent engine selection (healthiest, least loaded)
- Real-time throughput tracking (items/s per poll cycle)
- Completion detection with configurable poll threshold
- Orphan detection when pods restart
- Full session config persistence for retry/restart
- Tor sidecar integration with IP and connection status

---

## What's Next

- _Team roadmap items go here_

---

## Questions?

**Repository:** Amass Conductor
**Docs:** `docs/ARCHITECTURE.md` · `docs/USAGE_GUIDE.md` · `docs/ENGINE_API_GUIDE.md`
