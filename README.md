# Amass Conductor

Web UI for orchestrating [OWASP Amass](https://owasp.org/www-project-amass/) domain enumeration across Kubernetes engine pods. Amass Conductor discovers engine instances via the Kubernetes API, provides real-time session monitoring with WebSocket log streaming, manages the full session lifecycle (create, stop, retry, delete), and persists all results and logs to a local SQLite database.

## Features

- **Engine Auto-Discovery** — finds engine pods by Kubernetes label selector, tracks health and readiness
- **Real-Time Progress & Throughput** — live work-item counters and items/s rate updated every poll cycle
- **WebSocket Log Streaming** — aggregated log viewer with per-engine, per-session, and text filters
- **Tor Detection** — reads pod annotations from a Tor sidecar to show public IP and connection type
- **Scope Management** — supports 6 asset types (FQDN, IP Address, Netblock, Autonomous System, Location, Organization)
- **Session Lifecycle** — create, monitor, stop, retry, restart, and delete enumerations
- **Orphan Detection** — automatically marks sessions as failed when their engine pod disappears
- **Statistics Dashboard** — success rates, duration trends, throughput charts, and per-engine utilization

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 10.0 |
| UI Framework | Blazor Server (interactive SSR) |
| Component Library | Radzen.Blazor 9.x |
| Database | SQLite via EF Core 10.0 |
| Kubernetes | KubernetesClient 19.x |
| Logging | Serilog (Console + rolling JSON files) |
| Resilience | Microsoft.Extensions.Http.Resilience (Polly) |
| Config Parsing | YamlDotNet 16.x |
| Deployment | Docker / Kubernetes |

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Access to a Kubernetes cluster running Amass engine pods (or port-forward for local dev)

### Run locally

```bash
cd src/AmassOrchestrator/AmassOrchestrator.Web
dotnet run
```

The app starts on `http://localhost:5000` by default (see `Properties/launchSettings.json`).

### Docker

```bash
cd src/AmassOrchestrator
docker build -f AmassOrchestrator.Web/Dockerfile -t amass-conductor .
docker run -p 8080:8080 amass-conductor
```

### Run tests

```bash
cd src/AmassOrchestrator
dotnet test
```

## Configuration

All settings live under the `Orchestrator` section in `appsettings.json`:

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Namespace` | string | `amass` | Kubernetes namespace for engine pods |
| `StatefulSetName` | string | `amass-engine` | StatefulSet name to monitor |
| `EnginePort` | int | `4000` | Port where Amass Engine API listens |
| `PollIntervalSeconds` | int | `2` | Seconds between Kubernetes/engine polling cycles |
| `LabelSelector` | string | `app.kubernetes.io/name=amass,app.kubernetes.io/component=engine` | Label selector for pod discovery |
| `EngineBasePath` | string | `/api/v1` | Base path for engine REST API |
| `HttpClientTimeoutSeconds` | int | `10` | HTTP client timeout |
| `WebSocketBufferSize` | int | `4096` | WebSocket receive buffer size |
| `CompletionPollThreshold` | int | `5` | Consecutive polls with completed == total before marking done |
| `MaxActiveSessionsPerEngine` | int | `1` | Max concurrent sessions per engine pod |
| `AutoDeleteCompletedSessions` | bool | `true` | Auto-delete completed sessions from engine |
| `DatabasePath` | string | `data/orchestrator.db` | SQLite database file path |
| `SupportedDatabaseSystems` | string[] | `["postgres", "mysql"]` | Allowed database systems in config |
| `BruteForceWordlistFile` | string | `wordlists/default-namelist.txt` | Default brute-force wordlist |
| `AlterationsWordlistFile` | string | `wordlists/default-alterations.txt` | Default alterations wordlist |
| `DefaultTransformationsFile` | string | `wordlists/default-transformations.json` | Default transformations config |

Override via environment variables: `Orchestrator__Namespace=default` (double underscore for section separator).

## Documentation

- [Architecture Deep-Dive](docs/ARCHITECTURE.md) — system design, services, data flows
- [Usage Guide](docs/USAGE_GUIDE.md) — step-by-step UI walkthrough
- [Engine API Reference](docs/ENGINE_API_GUIDE.md) — REST and WebSocket API for engine pods
- [Presentation — General Overview](docs/PRESENTATION.md) — Marp slide deck for introductions
- [Presentation — Operations](docs/PRESENTATION-OPERATIONS.md) — Marp slide deck on session lifecycle and monitoring
- [Presentation — Architecture](docs/PRESENTATION-ARCHITECTURE.md) — Marp slide deck on technical design

## Project Structure

```
src/AmassOrchestrator/
├── AmassOrchestrator.Web/
│   ├── Components/
│   │   ├── Layout/          MainLayout, NavMenu
│   │   ├── Pages/           Dashboard, Sessions, SessionDetail, EngineDetail,
│   │   │                    Statistics, LiveLogs, Settings, CreateSessionDialog
│   │   └── Shared/          EngineCard, ProgressBar, HealthBadge, SessionControls,
│   │                        LogViewer, ScopeTable
│   ├── Configuration/       OrchestratorOptions
│   ├── Data/                SessionRecord, LogRecord, OrchestratorDbContext
│   ├── Models/              DTOs, view models, Kubernetes models
│   ├── Services/            EngineMonitorService, LogAggregatorService,
│   │                        EnumerationService, EngineStateStore,
│   │                        AmassEngineClient, KubernetesDiscoveryService,
│   │                        SessionRepository, DefaultsLoaderService
│   ├── wordlists/           Default wordlists and transformations
│   ├── Program.cs           DI registration and middleware
│   └── Dockerfile           Multi-stage .NET 10 build
└── AmassOrchestrator.Tests/
    ├── Models/              PodAnnotationInfoTests
    └── Services/            Unit tests for all core services
```
