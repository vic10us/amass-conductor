# Amass Engine API Guide

This guide documents the Amass Engine REST API and shows how to drive enumerations programmatically — replicating what the `enum` CLI subcommand does under the hood.

**Target CLI command being replicated:**

```bash
amass enum --config /.config/amass/config.yaml \
  -d fortifydata.com,fortifydata.cc,fortifydata.net \
  -engine "http://127.0.0.1:4000" -rigid -active
```

---

## Part 1: API Reference

### Endpoint Table

All endpoints are prefixed with `/api/v1`.

| # | Method | Path | Description | Success Code |
|---|--------|------|-------------|--------------|
| 1 | `GET` | `/health` | Health check | 200 |
| 2 | `POST` | `/sessions` | Create a new session | 201 |
| 3 | `GET` | `/sessions/list` | List active session tokens | 200 |
| 4 | `DELETE` | `/sessions/{token}` | Terminate a session | 204 |
| 5 | `GET` | `/sessions/{token}/stats` | Get session statistics | 200 |
| 6 | `GET` | `/sessions/{token}/scope/{asset_type}` | Get scoped assets by type | 200 |
| 7 | `POST` | `/sessions/{token}/assets/{asset_type}` | Add a single asset | 200 |
| 8 | `POST` | `/sessions/{token}/assets/{asset_type}:bulk` | Add assets in bulk | 200 |
| 9 | `GET` (WebSocket) | `/sessions/{token}/ws/logs` | Subscribe to log messages | 101 |

`{token}` is a UUID returned by `POST /sessions`.
`{asset_type}` is a lowercase string — see the [Asset Type Reference](#asset-type-reference) below.

### Configuration JSON Structure

The `POST /sessions` body is a JSON serialization of the `config.Config` struct. Below are the fields that serialize to JSON (fields tagged `json:"-"` are omitted by the marshaller and not shown).

```jsonc
{
  // Scope
  "scope": {
    "domains": ["fortifydata.com", "fortifydata.cc", "fortifydata.net"],
    "ips": ["192.0.2.1"],           // optional
    "asns": [13335],                // optional
    "cidrs": ["192.0.2.0/24"],      // optional
    "ports": [80, 443],             // optional
    "blacklist": ["bad.example.com"] // optional
  },
  "seed": {                          // optional seed scope
    "domains": [],
    "ips": [],
    "asns": [],
    "cidrs": [],
    "ports": [],
    "blacklist": []
  },

  // Enumeration behaviour
  "active": true,
  "rigid_boundaries": true,
  "brute_force": false,              // optional
  "alterations": false,              // optional
  "wordlist": [],                    // optional — brute-force wordlist
  "alt_worldlist": [],               // optional — alterations wordlist (note: typo is intentional, matches codebase)
  "resolvers": ["8.8.8.8"],         // optional
  "trusted_resolvers": ["1.1.1.1"], // optional

  // Database (optional)
  "database": [
    {
      "system": "postgres",
      "primary": true,
      "url": "postgres://amass:CHANGE_ME@localhost:5432/assetdb",
      "username": "amass",
      "password": "CHANGE_ME",
      "host": "localhost",
      "port": "5432",
      "db_name": "assetdb",
      "options": ""
    }
  ],

  // Data source configuration (optional)
  "datasource_config": {
    "datasources": [],
    "global_options": {}
  },

  // Transformations — keys are "FromType->ToType" or "FromType->ALL"
  "transformations": {
    "FQDN->ALL": { "priority": 5, "confidence": 50, "ttl": 1440 },
    "FQDN->DomainRecord": { "ttl": 43200 },
    "IPAddress->ALL": {},
    "AutonomousSystem->ALL": {},
    "AutonomousSystem->RDAP": { "ttl": 43200 },
    "Netblock->ALL": {},
    "Netblock->RDAP": { "ttl": 43200 },
    "Organization->ALL": {},
    "Organization->GLEIF": { "ttl": 43200 },
    "Location->ALL": {},
    "DomainRecord->ALL": {},
    "ContactRecord->ALL": {},
    "Account->ALL": { "ttl": 10800 },
    "AutnumRecord->ALL": {},
    "File->ALL": {},
    "FundsTransfer->ALL": { "ttl": 10800 },
    "Identifier->ALL": {},
    "Identifier->GLEIF": { "ttl": 43200 },
    "Identifier->RDAP": { "ttl": 43200 },
    "IPNetRecord->ALL": {},
    "Person->ALL": { "ttl": 43200 },
    "Phone->ALL": { "ttl": 10800 },
    "Product->ALL": { "ttl": 10800 },
    "ProductRelease->ALL": {},
    "Service->ALL": {},
    "TLSCertificate->ALL": { "ttl": 10800 },
    "URL->ALL": {}
  }
}
```

**Transformation fields:**

| Field | Type | Description |
|-------|------|-------------|
| `priority` | int | Processing priority (default: 5) |
| `confidence` | int | Confidence percentage (default: 50) |
| `ttl` | int | Time-to-live in minutes (default: 1440 = 1 day) |
| `exclude` | string[] | Data sources to exclude for this transformation |

### Asset Type Reference

All 21 supported asset types. The **URL path value** is the lowercase string used in endpoint paths.

| Asset Type | URL Path Value | Example JSON Body |
|------------|---------------|-------------------|
| Account | `account` | `{"unique_id":"user@example.com"}` |
| AutnumRecord | `autnumrecord` | `{"handle":"AS13335"}` |
| AutonomousSystem | `autonomoussystem` | `{"number":13335}` |
| ContactRecord | `contactrecord` | `{"discovered_at":"2024-01-01T00:00:00Z"}` |
| DomainRecord | `domainrecord` | `{"domain":"example.com"}` |
| File | `file` | `{"url":"https://example.com/file.txt"}` |
| FQDN | `fqdn` | `{"name":"www.example.com"}` |
| FundsTransfer | `fundstransfer` | `{"unique_id":"tx-123"}` |
| Identifier | `identifier` | `{"unique_id":"LEI-123"}` |
| IPAddress | `ipaddress` | `{"address":"192.0.2.1","type":"IPv4"}` |
| IPNetRecord | `ipnetrecord` | `{"handle":"NET-192-0-2-0-1"}` |
| Location | `location` | `{"address":"123 Main St"}` |
| Netblock | `netblock` | `{"cidr":"192.0.2.0/24","type":"IPv4"}` |
| Organization | `organization` | `{"name":"Example Inc"}` |
| Person | `person` | `{"unique_id":"person-123"}` |
| Phone | `phone` | `{"e164":"+15551234567"}` |
| Product | `product` | `{"unique_id":"prod-123"}` |
| ProductRelease | `productrelease` | `{"name":"v1.0.0"}` |
| Service | `service` | `{"unique_id":"svc-123"}` |
| TLSCertificate | `tlscertificate` | `{"serial_number":"01:23:45"}` |
| URL | `url` | `{"url":"https://example.com"}` |

**Scope endpoint limitation:** `GET /sessions/{token}/scope/{asset_type}` only supports 6 asset types:
`fqdn`, `ipaddress`, `netblock`, `autonomoussystem`, `location`, `organization`.

### Error Response Format

All error responses use this shape:

```json
{
  "error": "human-readable message",
  "details": "underlying error string (omitted when empty)",
  "code": 400
}
```

---

## Part 2: Step-by-Step Walkthrough (curl)

Variables used throughout:

```bash
ENGINE="http://127.0.0.1:4000"
```

### Step 1: Health Check

```bash
curl -s "${ENGINE}/api/v1/health" | jq .
```

Expected response:

```json
{"result": "Amass Engine OK"}
```

### Step 2: Create Session

```bash
TOKEN=$(curl -s -X POST "${ENGINE}/api/v1/sessions" \
  -H "Content-Type: application/json" \
  -d @- <<'EOF' | jq -r '.sessionToken'
{
  "scope": {
    "domains": ["fortifydata.com", "fortifydata.cc", "fortifydata.net"],
    "ports": [80, 443]
  },
  "active": true,
  "rigid_boundaries": true,
  "resolvers": ["8.8.8.8", "8.8.4.4"],
  "transformations": {
    "FQDN->ALL": {"ttl": 1440, "confidence": 50, "priority": 5},
    "FQDN->DomainRecord": {"ttl": 43200},
    "IPAddress->ALL": {},
    "AutonomousSystem->ALL": {},
    "AutonomousSystem->RDAP": {"ttl": 43200},
    "Netblock->ALL": {},
    "Netblock->RDAP": {"ttl": 43200},
    "Organization->ALL": {},
    "Organization->GLEIF": {"ttl": 43200},
    "Location->ALL": {},
    "DomainRecord->ALL": {},
    "ContactRecord->ALL": {},
    "Account->ALL": {"ttl": 10800},
    "AutnumRecord->ALL": {},
    "File->ALL": {},
    "FundsTransfer->ALL": {"ttl": 10800},
    "Identifier->ALL": {},
    "Identifier->GLEIF": {"ttl": 43200},
    "Identifier->RDAP": {"ttl": 43200},
    "IPNetRecord->ALL": {},
    "Person->ALL": {"ttl": 43200},
    "Phone->ALL": {"ttl": 10800},
    "Product->ALL": {"ttl": 10800},
    "ProductRelease->ALL": {},
    "Service->ALL": {},
    "TLSCertificate->ALL": {"ttl": 10800},
    "URL->ALL": {}
  }
}
EOF
)

echo "Session token: ${TOKEN}"
```

### Step 3: Subscribe to Log Messages (WebSocket)

```bash
# Using websocat (install: cargo install websocat)
websocat "ws://${ENGINE#http://}/api/v1/sessions/${TOKEN}/ws/logs" &
WS_PID=$!
```

The server sends ping frames every 30 seconds. Clients must respond with pong within 60 seconds or the connection is closed.

### Step 4: Submit Scope Assets

Submit each domain as an FQDN asset:

```bash
for DOMAIN in fortifydata.com fortifydata.cc fortifydata.net; do
  curl -s -X POST "${ENGINE}/api/v1/sessions/${TOKEN}/assets/fqdn" \
    -H "Content-Type: application/json" \
    -d "{\"name\":\"${DOMAIN}\"}" | jq .
done
```

Expected response per domain:

```json
{"entityID": "some-entity-id"}
```

### Step 5: Monitor Progress

Poll session stats every 2 seconds:

```bash
curl -s "${ENGINE}/api/v1/sessions/${TOKEN}/stats" | jq .
```

Response:

```json
{
  "workItemsCompleted": 42,
  "workItemsTotal": 100
}
```

The enumeration is complete when `workItemsCompleted == workItemsTotal` for **5 consecutive polls** (10 seconds of stability).

### Step 6: Get Results

Retrieve discovered assets for each scope type:

```bash
for TYPE in fqdn ipaddress netblock autonomoussystem location organization; do
  echo "=== ${TYPE} ==="
  curl -s "${ENGINE}/api/v1/sessions/${TOKEN}/scope/${TYPE}" | jq '.data[]' 2>/dev/null
done
```

Response shape:

```json
{
  "data": [
    {"name": "www.fortifydata.com"},
    {"name": "mail.fortifydata.com"}
  ]
}
```

### Step 7: Terminate Session

```bash
curl -s -X DELETE "${ENGINE}/api/v1/sessions/${TOKEN}"
# Returns 204 No Content on success
```

---

## Part 3: Complete Bash Script

```bash
#!/usr/bin/env bash
set -euo pipefail

# ── Configuration ──────────────────────────────────────────────────
ENGINE="${ENGINE:-http://127.0.0.1:4000}"
DOMAINS=("fortifydata.com" "fortifydata.cc" "fortifydata.net")
POLL_INTERVAL=2
TIMEOUT_MINUTES=30
CONSECUTIVE_CHECKS_NEEDED=5
SCOPE_TYPES=("fqdn" "ipaddress" "netblock" "autonomoussystem" "location" "organization")

CONFIG_JSON=$(cat <<'ENDJSON'
{
  "scope": {
    "domains": ["fortifydata.com", "fortifydata.cc", "fortifydata.net"],
    "ports": [80, 443]
  },
  "active": true,
  "rigid_boundaries": true,
  "transformations": {
    "FQDN->ALL": {"ttl": 1440, "confidence": 50, "priority": 5},
    "FQDN->DomainRecord": {"ttl": 43200},
    "IPAddress->ALL": {},
    "AutonomousSystem->ALL": {},
    "AutonomousSystem->RDAP": {"ttl": 43200},
    "Netblock->ALL": {},
    "Netblock->RDAP": {"ttl": 43200},
    "Organization->ALL": {},
    "Organization->GLEIF": {"ttl": 43200},
    "Location->ALL": {},
    "DomainRecord->ALL": {},
    "ContactRecord->ALL": {},
    "Account->ALL": {"ttl": 10800},
    "AutnumRecord->ALL": {},
    "File->ALL": {},
    "FundsTransfer->ALL": {"ttl": 10800},
    "Identifier->ALL": {},
    "Identifier->GLEIF": {"ttl": 43200},
    "Identifier->RDAP": {"ttl": 43200},
    "IPNetRecord->ALL": {},
    "Person->ALL": {"ttl": 43200},
    "Phone->ALL": {"ttl": 10800},
    "Product->ALL": {"ttl": 10800},
    "ProductRelease->ALL": {},
    "Service->ALL": {},
    "TLSCertificate->ALL": {"ttl": 10800},
    "URL->ALL": {}
  }
}
ENDJSON
)

# ── Helpers ────────────────────────────────────────────────────────
cleanup() {
  if [[ -n "${TOKEN:-}" ]]; then
    echo "Terminating session ${TOKEN}..."
    curl -sf -X DELETE "${ENGINE}/api/v1/sessions/${TOKEN}" || true
  fi
  if [[ -n "${WS_PID:-}" ]] && kill -0 "$WS_PID" 2>/dev/null; then
    kill "$WS_PID" 2>/dev/null || true
  fi
}
trap cleanup EXIT

die() { echo "ERROR: $*" >&2; exit 1; }

# ── Step 1: Health check ──────────────────────────────────────────
echo "Checking engine health..."
curl -sf "${ENGINE}/api/v1/health" > /dev/null || die "Engine not reachable at ${ENGINE}"
echo "Engine is healthy."

# ── Step 2: Create session ────────────────────────────────────────
echo "Creating session..."
RESPONSE=$(curl -sf -X POST "${ENGINE}/api/v1/sessions" \
  -H "Content-Type: application/json" \
  -d "${CONFIG_JSON}") || die "Failed to create session"

TOKEN=$(echo "${RESPONSE}" | jq -r '.sessionToken')
[[ "${TOKEN}" != "null" && -n "${TOKEN}" ]] || die "No session token in response: ${RESPONSE}"
echo "Session created: ${TOKEN}"

# ── Step 3: Subscribe to logs (background) ────────────────────────
LOG_FILE="session-${TOKEN}.log"
if command -v websocat &>/dev/null; then
  WS_URL="ws://${ENGINE#http://}/api/v1/sessions/${TOKEN}/ws/logs"
  websocat "${WS_URL}" >> "${LOG_FILE}" 2>/dev/null &
  WS_PID=$!
  echo "Log stream writing to ${LOG_FILE} (PID: ${WS_PID})"
else
  WS_PID=""
  echo "Warning: websocat not found — log streaming skipped."
fi

# ── Step 4: Submit scope assets ───────────────────────────────────
ASSET_COUNT=0
for DOMAIN in "${DOMAINS[@]}"; do
  RESULT=$(curl -sf -X POST "${ENGINE}/api/v1/sessions/${TOKEN}/assets/fqdn" \
    -H "Content-Type: application/json" \
    -d "{\"name\":\"${DOMAIN}\"}") || { echo "Warning: failed to submit ${DOMAIN}"; continue; }
  ASSET_COUNT=$((ASSET_COUNT + 1))
  echo "Submitted: ${DOMAIN}"
done
echo "Submitted ${ASSET_COUNT} assets."

# ── Step 5: Poll for completion ───────────────────────────────────
echo "Polling for completion (timeout: ${TIMEOUT_MINUTES}m)..."
TIMEOUT_SECS=$((TIMEOUT_MINUTES * 60))
START_TIME=$(date +%s)
LAST_PROGRESS_TIME=${START_TIME}
PREVIOUS_COMPLETED=-1
FINISHED_COUNT=0

while true; do
  sleep "${POLL_INTERVAL}"

  NOW=$(date +%s)
  ELAPSED=$((NOW - LAST_PROGRESS_TIME))
  if (( ELAPSED >= TIMEOUT_SECS )); then
    echo "Timeout: no progress for ${TIMEOUT_MINUTES} minutes."
    break
  fi

  STATS=$(curl -sf "${ENGINE}/api/v1/sessions/${TOKEN}/stats" 2>/dev/null) || continue
  COMPLETED=$(echo "${STATS}" | jq -r '.workItemsCompleted')
  TOTAL=$(echo "${STATS}" | jq -r '.workItemsTotal')

  # Reset timeout on progress
  if [[ "${COMPLETED}" != "${PREVIOUS_COMPLETED}" ]]; then
    PREVIOUS_COMPLETED="${COMPLETED}"
    LAST_PROGRESS_TIME=$(date +%s)
  fi

  echo -ne "\rProgress: ${COMPLETED}/${TOTAL}  "

  # 5 consecutive checks where completed == total
  if [[ "${COMPLETED}" == "${TOTAL}" ]]; then
    FINISHED_COUNT=$((FINISHED_COUNT + 1))
    if (( FINISHED_COUNT >= CONSECUTIVE_CHECKS_NEEDED )); then
      echo -e "\nEnumeration complete."
      break
    fi
  else
    FINISHED_COUNT=0
  fi
done

# ── Step 6: Get results ──────────────────────────────────────────
echo ""
echo "Session Scope"
for TYPE in "${SCOPE_TYPES[@]}"; do
  RESULT=$(curl -sf "${ENGINE}/api/v1/sessions/${TOKEN}/scope/${TYPE}" 2>/dev/null) || continue
  echo ""
  echo "${TYPE}:"
  echo ""
  echo "${RESULT}" | jq -r '.data[] | if .name then .name elif .legal_name then .legal_name elif .address then .address elif .cidr then .cidr elif .number then (.number | tostring) else (. | tostring) end' 2>/dev/null
done

# ── Step 7: Terminate session (handled by trap) ──────────────────
echo ""
echo "Done."
```

---

## Part 4: Complete Python Script

Requires: `pip install requests websocket-client`

```python
#!/usr/bin/env python3
"""
Amass Engine API enumeration script.

Replicates the CLI workflow:
  amass enum -d fortifydata.com,fortifydata.cc,fortifydata.net \
    -engine http://127.0.0.1:4000 -rigid -active
"""

import json
import sys
import threading
import time

import requests
import websocket

# ── Configuration ─────────────────────────────────────────────────
ENGINE = "http://127.0.0.1:4000"
API_BASE = f"{ENGINE}/api/v1"
DOMAINS = ["fortifydata.com", "fortifydata.cc", "fortifydata.net"]
POLL_INTERVAL = 2  # seconds
TIMEOUT_MINUTES = 30
CONSECUTIVE_CHECKS_NEEDED = 5
SCOPE_TYPES = ["fqdn", "ipaddress", "netblock", "autonomoussystem", "location", "organization"]

SESSION_CONFIG = {
    "scope": {
        "domains": DOMAINS,
        "ports": [80, 443],
    },
    "active": True,
    "rigid_boundaries": True,
    "transformations": {
        "FQDN->ALL": {"ttl": 1440, "confidence": 50, "priority": 5},
        "FQDN->DomainRecord": {"ttl": 43200},
        "IPAddress->ALL": {},
        "AutonomousSystem->ALL": {},
        "AutonomousSystem->RDAP": {"ttl": 43200},
        "Netblock->ALL": {},
        "Netblock->RDAP": {"ttl": 43200},
        "Organization->ALL": {},
        "Organization->GLEIF": {"ttl": 43200},
        "Location->ALL": {},
        "DomainRecord->ALL": {},
        "ContactRecord->ALL": {},
        "Account->ALL": {"ttl": 10800},
        "AutnumRecord->ALL": {},
        "File->ALL": {},
        "FundsTransfer->ALL": {"ttl": 10800},
        "Identifier->ALL": {},
        "Identifier->GLEIF": {"ttl": 43200},
        "Identifier->RDAP": {"ttl": 43200},
        "IPNetRecord->ALL": {},
        "Person->ALL": {"ttl": 43200},
        "Phone->ALL": {"ttl": 10800},
        "Product->ALL": {"ttl": 10800},
        "ProductRelease->ALL": {},
        "Service->ALL": {},
        "TLSCertificate->ALL": {"ttl": 10800},
        "URL->ALL": {},
    },
}


def health_check():
    """Step 1: Verify the engine is reachable."""
    resp = requests.get(f"{API_BASE}/health", timeout=10)
    resp.raise_for_status()
    print("Engine is healthy.")


def create_session() -> str:
    """Step 2: Create a session and return the token."""
    resp = requests.post(
        f"{API_BASE}/sessions",
        json=SESSION_CONFIG,
        headers={"Content-Type": "application/json"},
        timeout=60,
    )
    resp.raise_for_status()
    token = resp.json()["sessionToken"]
    print(f"Session created: {token}")
    return token


def subscribe_logs(token: str, stop_event: threading.Event) -> threading.Thread:
    """Step 3: Subscribe to log messages in a background thread."""
    ws_url = f"ws://{ENGINE.split('://', 1)[1]}/api/v1/sessions/{token}/ws/logs"
    log_file = f"session-{token}.log"

    def _on_message(ws, message):
        with open(log_file, "a") as f:
            f.write(message + "\n")

    def _on_error(ws, error):
        print(f"WebSocket error: {error}", file=sys.stderr)

    def _on_close(ws, close_status_code, close_msg):
        pass

    def _run():
        ws = websocket.WebSocketApp(
            ws_url,
            on_message=_on_message,
            on_error=_on_error,
            on_close=_on_close,
        )
        ws.run_forever(ping_interval=30, ping_timeout=60)

    t = threading.Thread(target=_run, daemon=True)
    t.start()
    print(f"Log stream writing to {log_file}")
    return t


def submit_assets(token: str) -> int:
    """Step 4: Submit scope domains as FQDN assets."""
    count = 0
    for domain in DOMAINS:
        resp = requests.post(
            f"{API_BASE}/sessions/{token}/assets/fqdn",
            json={"name": domain},
            headers={"Content-Type": "application/json"},
            timeout=2,
        )
        if resp.ok:
            count += 1
            print(f"Submitted: {domain}")
        else:
            print(f"Warning: failed to submit {domain}: {resp.text}", file=sys.stderr)
    print(f"Submitted {count} assets.")
    return count


def poll_until_complete(token: str):
    """Step 5: Poll stats until completion or timeout."""
    timeout_secs = TIMEOUT_MINUTES * 60
    last_progress_time = time.monotonic()
    previous_completed = -1
    finished_count = 0

    print(f"Polling for completion (timeout: {TIMEOUT_MINUTES}m)...")

    while True:
        time.sleep(POLL_INTERVAL)

        elapsed = time.monotonic() - last_progress_time
        if elapsed >= timeout_secs:
            print(f"\nTimeout: no progress for {TIMEOUT_MINUTES} minutes.")
            return

        try:
            resp = requests.get(f"{API_BASE}/sessions/{token}/stats", timeout=2)
            resp.raise_for_status()
            stats = resp.json()
        except Exception:
            continue

        completed = stats.get("workItemsCompleted", 0)
        total = stats.get("workItemsTotal", 0)

        # Reset timeout on progress
        if completed != previous_completed:
            previous_completed = completed
            last_progress_time = time.monotonic()

        print(f"\rProgress: {completed}/{total}  ", end="", flush=True)

        # 5 consecutive checks where completed == total
        if completed == total:
            finished_count += 1
            if finished_count >= CONSECUTIVE_CHECKS_NEEDED:
                print("\nEnumeration complete.")
                return
        else:
            finished_count = 0


def get_results(token: str):
    """Step 6: Retrieve and print scope results."""
    print("\nSession Scope")
    for asset_type in SCOPE_TYPES:
        try:
            resp = requests.get(
                f"{API_BASE}/sessions/{token}/scope/{asset_type}", timeout=5
            )
            if not resp.ok:
                continue
            data = resp.json().get("data", [])
        except Exception:
            continue

        if not data:
            continue

        print(f"\n{asset_type}:\n")
        for item in data:
            # Pick the most useful display field
            name = (
                item.get("name")
                or item.get("legal_name")
                or item.get("address")
                or item.get("cidr")
                or str(item.get("number", ""))
                or json.dumps(item)
            )
            print(f"  {name}")


def terminate_session(token: str):
    """Step 7: Terminate the session."""
    try:
        requests.delete(f"{API_BASE}/sessions/{token}", timeout=5)
        print(f"Session {token} terminated.")
    except Exception as e:
        print(f"Warning: failed to terminate session: {e}", file=sys.stderr)


def main():
    token = None
    stop_event = threading.Event()

    try:
        health_check()
        token = create_session()
        subscribe_logs(token, stop_event)
        submit_assets(token)
        poll_until_complete(token)
        get_results(token)
    except KeyboardInterrupt:
        print("\nInterrupted.")
    except Exception as e:
        print(f"Fatal: {e}", file=sys.stderr)
        sys.exit(1)
    finally:
        stop_event.set()
        if token:
            terminate_session(token)


if __name__ == "__main__":
    main()
```

---

## Part 5: Notes

### Completion Detection Logic

The CLI (and the scripts above) consider an enumeration "done" when `workItemsCompleted == workItemsTotal` for **5 consecutive** stat polls (every 2 seconds). This guards against a transient race where the counter briefly matches before new work items are queued. If no progress is observed within the timeout period (default: 30 minutes), the session is terminated.

From `internal/enum/cli.go`:

```go
if stats.WorkItemsCompleted == stats.WorkItemsTotal {
    finished++
    if finished == 5 {
        close(done)
        return
    }
} else {
    finished = 0
}
```

### Bulk Upload Limits

| Layer | Max Items Per Request |
|-------|---------------------|
| Server (`handlers.go`) | 5,000 |
| Go client library (`client.go`) | 1,000 |

When submitting large numbers of pre-known names, batch into chunks of up to 1,000 and use the `:bulk` endpoint:

```bash
curl -X POST "${ENGINE}/api/v1/sessions/${TOKEN}/assets/fqdn:bulk" \
  -H "Content-Type: application/json" \
  -d '{"items":[{"name":"a.example.com"},{"name":"b.example.com"}]}'
```

Response:

```json
{"ingested": 2, "stored": 2, "failed": 0}
```

### WebSocket Ping/Pong Requirements

The server-side WebSocket handler:
- Sends **ping** frames every **30 seconds**
- Sets a **60-second read deadline** that resets on each pong
- Read limit: 1 MB

Clients must respond to ping frames with pong frames. Most WebSocket libraries handle this automatically. If using a raw connection, ensure pong handling is implemented.

### The `alt_worldlist` Typo

The JSON tag for the alterations wordlist field is `alt_worldlist` (with a **d** — "worldlist" not "wordlist"). This is **intentional** — it matches the codebase (`config/config.go` line 105) and the server will only recognize this exact spelling:

```go
AltWordlist []string `yaml:"-" json:"alt_worldlist,omitempty"`
```

### Scope Endpoint Asset Type Limitation

The `GET /sessions/{token}/scope/{asset_type}` endpoint only has switch-case handling for 6 asset types:

- `fqdn`
- `ipaddress`
- `netblock`
- `autonomoussystem`
- `location`
- `organization`

Requesting any other asset type will return a 404 with `"session scope not found for the selected asset type"`.

### How CLI Flags Map to Config JSON

| CLI Flag | Config JSON Field |
|----------|------------------|
| `-d fortifydata.com` | `scope.domains` |
| `-active` | `active: true` |
| `-rigid` | `rigid_boundaries: true` |
| `-config config.yaml` | Entire config body (transformations, etc.) |
| `-engine http://...` | Client-side only (not in config JSON) |
| `-brute` | `brute_force: true` |
| `-alts` | `alterations: true` |
| `-w wordlist.txt` | `wordlist: [...]` |
| `-aw altwordlist.txt` | `alt_worldlist: [...]` |
| `-r 8.8.8.8` | `resolvers: ["8.8.8.8"]` |
| `-asn 13335` | `scope.asns: [13335]` |
| `-cidr 192.0.2.0/24` | `scope.cidrs: ["192.0.2.0/24"]` |
| `-addr 192.0.2.1` | `scope.ips: ["192.0.2.1"]` |
| `-p 8080` | `scope.ports: [8080]` |
| `-bl bad.example.com` | `scope.blacklist: ["bad.example.com"]` |
