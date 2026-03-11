# Usage Guide

Step-by-step walkthrough of the Amass Conductor web UI.

## Accessing the Application

Open your browser to the configured URL (default `http://localhost:8080` in Docker, `http://localhost:5000` in local dev).

The sidebar navigation provides access to all sections:

| Nav Item | Route | Description |
|----------|-------|-------------|
| Dashboard | `/` | Engine overview and quick session creation |
| Sessions | `/sessions` | Session list with filtering and bulk operations |
| Logs | `/logs` | Aggregated log viewer |
| Statistics | `/statistics` | Historical metrics and charts |
| Settings | `/settings` | Runtime configuration |

## Dashboard

The landing page displays a card for each discovered engine pod.

**Engine cards show:**
- Pod name and IP address
- Phase (Running, Pending, etc.) and public IP (if available)
- Health badge (Healthy / Unhealthy / Not Ready)
- Tor/Direct connection badge (from pod annotations)
- Active session count
- Aggregated progress bar with throughput when sessions are running

**Actions:**
- Click any engine card to navigate to the engine detail page
- Click **New Enumeration** in the top-right to open the session creation dialog

## Creating an Enumeration

The **Create Session** dialog is a 6-tab form covering all Amass configuration options.

### Scope Tab

Enter the domains to enumerate, one per line or comma-separated.

```
example.com
subdomain.example.org
```

At least one domain is required.

### Options Tab

Toggle enumeration techniques:

| Option | Description |
|--------|-------------|
| Active | Enable active DNS probing (noisier but finds more) |
| Brute Force | Use wordlist-based subdomain guessing |
| Alterations | Try permutations of discovered subdomains |
| Rigid Boundaries | Stay strictly within provided scope |

### Wordlists Tab

Customize wordlists loaded from the server defaults:

- **Brute Force Wordlist** — one entry per line, used when Brute Force is enabled
- **Alterations Wordlist** — one entry per line, used when Alterations is enabled

Default wordlists are loaded from `wordlists/default-namelist.txt` and `wordlists/default-alterations.txt`.

### Database Tab

Optionally configure a database for Amass to store results:

- **Database URL** — paste a connection string (`postgres://user:pass@host:5432/dbname`) and fields auto-populate
- **System** — postgres or mysql
- **Host, Port, Database Name, Username, Password** — individual connection fields
- **Primary** — mark as primary database
- **Options** — additional connection options

### Datasources Tab

Paste YAML configuration for data sources (API keys for VirusTotal, Shodan, etc.):

```yaml
SecurityTrails:
  - api_key: YOUR_KEY
Shodan:
  - api_key: YOUR_KEY
```

Invalid YAML shows an inline error message.

### Transformations Tab

Configure which transformations Amass should apply:

- Data grid lists all available transformations with:
  - **Enabled** checkbox
  - **Name** (e.g., `dns-fqdn-to-ipaddress`)
  - **Priority** (integer)
  - **Confidence** (0–100)
  - **TTL** (seconds, 0 = no cache)
- Add custom transformations with the input field at the bottom

### Submitting

- **Auto-select** (default): Conductor picks the healthiest engine with capacity
- **Specific engine**: when restarting, choose the same engine or a different one

Click **Create** to start the enumeration.

## Monitoring Sessions

### Sessions List (`/sessions`)

The sessions page shows all sessions (active and historical) in a filterable data grid.

**Filters:**
- **Status** dropdown — All, Active, Completed, Failed, Cancelled
- **Engine** dropdown — filter by specific engine pod
- **Domain** search — text filter across session domains

**Grid columns:**
- Checkbox (for bulk selection)
- Token (click to navigate to session detail)
- Engine pod name
- Domains
- Progress bar with completion percentage and items/s
- Status badge (color-coded)
- Created time (relative, e.g., "5m ago")
- Actions (context-sensitive: Retry, Delete)

**Bulk operations** (appear when rows are selected):
- **Stop Selected** — cancel all selected active sessions
- **Delete Selected** — remove selected completed/failed/cancelled sessions
- **Restart Selected** — re-create selected sessions with their saved configurations

### Session Detail (`/engine/{PodName}/session/{SessionToken}`)

Detailed view of a single session with 4 tabs.

**Header:** session token (abbreviated), status badges, and action buttons (Stop for active sessions).

**Stats tab:**
- Progress bar with work items completed/total
- Throughput rate (items/s)
- Completion percentage
- Error message (if failed)

**Logs tab:**
- For active sessions: real-time log viewer with WebSocket streaming (last 500 lines)
- For historical sessions: persisted logs from database
- Reconnect button if WebSocket disconnects

**Scope tab:**
- Paginated tables for each of the 6 asset types:
  - FQDN, IP Address, Netblock, Autonomous System, Location, Organization
- Refresh button per table
- Shows discovered assets in the scope

**Configuration tab:**
- Full enumeration configuration as submitted:
  - Domains list
  - Options flags (Active, Brute Force, Alterations, Rigid Boundaries)
  - Wordlists (truncated display)
  - Database connection (password masked)
  - Datasources (YAML)
  - Transformations (enabled items with priority/confidence/TTL)

## Engine Detail (`/engine/{PodName}`)

Detailed view of a specific engine pod.

**Displays:**
- Pod name, health badge, Tor status badge
- Network info card: public IP, check status, connection type (Tor/Direct), last checked
- Active sessions in a data grid with progress bars

**Actions:**
- **Create Session** — open dialog targeting this specific engine
- **New Enumeration (Auto)** — open dialog with auto-engine selection
- Click any session row to navigate to session detail

## Logs (`/logs`)

Aggregated log viewer combining historical database logs and live WebSocket streams.

**Filters:**
- **Engine** dropdown — filter by specific engine pod
- **Session** dropdown — filter by specific session token
- **Search** text box — filter log messages by text content
- **Include Live** checkbox — toggle real-time log streaming
- **Auto-scroll** checkbox — auto-scroll to latest entries

Logs display in a scrollable pre-formatted area, formatted as `[EngineName/SessionToken] message`.

Live logs maintain the last 1,000 entries in memory.

## Statistics (`/statistics`)

Historical analytics dashboard.

**Summary cards:**
- Total Sessions, Completed, Failed, Active
- Work Items Processed, Success Rate %
- Average Duration (minutes), Min/Max Duration
- Average Throughput (items/s)

**Charts:**
- Sessions over last 30 days (column chart)
- Success vs Failure distribution (pie chart)
- Duration per session — last 20 completed (bar chart)
- Throughput per session — last 20 completed (bar chart)
- Work items per session — last 20 (column chart)
- Sessions per engine (pie chart)

## Settings (`/settings`)

Runtime configuration for the orchestrator. Changes apply immediately in memory but are **not persisted** to `appsettings.json`.

**Configurable fields:**
- Kubernetes Namespace
- StatefulSet Name
- Engine Port
- Poll Interval (seconds)
- Max Active Sessions Per Engine

**Read-only fields:**
- Database Path

## Session Lifecycle Operations

| Operation | Applies To | What It Does |
|-----------|-----------|--------------|
| **Stop** | Active sessions | Sends cancellation, marks session as cancelled in DB |
| **Retry** | Completed, Failed, Cancelled | Re-creates session with saved config (same or different engine) |
| **Restart** | Same as Retry | Alias for Retry with engine selection dialog |
| **Delete** | Completed, Failed, Cancelled | Removes session record and logs from database |

Stop requires confirmation before executing. Retry/Restart opens the Create Session dialog pre-populated with the original configuration.
