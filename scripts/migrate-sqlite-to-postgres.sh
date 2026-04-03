#!/usr/bin/env bash
# migrate-sqlite-to-postgres.sh — migrate the orchestrator SQLite database to PostgreSQL
#
# Usage:
#   ./scripts/migrate-sqlite-to-postgres.sh \
#     --sqlite  /app/data/orchestrator.db \
#     --pg-host localhost \
#     --pg-port 5432 \
#     --pg-db   orchestrator \
#     --pg-user orchestrator \
#     --pg-pass secret
#
# Prerequisites:
#   - pgloader installed (https://pgloader.io, or: apt install pgloader)
#   - PostgreSQL database created and schema already initialized:
#       psql -U postgres -v db_password="'<pw>'" -f scripts/create-orchestrator-db.sql
#   - App started once against PostgreSQL (creates tables via EnsureCreatedAsync), then STOPPED.
#   - The orchestrator app must NOT be running while this script executes.

set -euo pipefail

SQLITE_PATH="/app/data/orchestrator.db"
PG_HOST=""
PG_PORT="5432"
PG_DB="orchestrator"
PG_USER="orchestrator"
PG_PASS=""

usage() {
    echo "Usage: $0 --sqlite <path> --pg-host <host> --pg-port <port> --pg-db <db> --pg-user <user> --pg-pass <pass>"
    exit 1
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --sqlite)   SQLITE_PATH="$2"; shift 2 ;;
        --pg-host)  PG_HOST="$2";     shift 2 ;;
        --pg-port)  PG_PORT="$2";     shift 2 ;;
        --pg-db)    PG_DB="$2";       shift 2 ;;
        --pg-user)  PG_USER="$2";     shift 2 ;;
        --pg-pass)  PG_PASS="$2";     shift 2 ;;
        *) echo "Unknown option: $1"; usage ;;
    esac
done

[[ -z "$SQLITE_PATH" ]] && { echo "ERROR: --sqlite is required"; usage; }
[[ -z "$PG_PASS" ]]    && { echo "ERROR: --pg-pass is required"; usage; }
[[ ! -f "$SQLITE_PATH" ]] && { echo "ERROR: SQLite file not found: $SQLITE_PATH"; exit 1; }

command -v pgloader >/dev/null 2>&1 || { echo "ERROR: pgloader not found. Install with: apt install pgloader"; exit 1; }
command -v psql     >/dev/null 2>&1 || { echo "ERROR: psql not found."; exit 1; }

PG_URI="postgresql://${PG_USER}:${PG_PASS}@${PG_HOST}:${PG_PORT}/${PG_DB}"
LOAD_FILE="$(mktemp /tmp/pgloader-orchestrator-XXXXXX.load)"
trap 'rm -f "$LOAD_FILE"' EXIT

cat > "$LOAD_FILE" <<PGLOADER
LOAD DATABASE
     FROM sqlite:///${SQLITE_PATH}
     INTO ${PG_URI}

WITH truncate,
     create no tables,
     create no indexes,
     reset sequences,
     quote identifiers,
     workers = 4, concurrency = 1

CAST
     column "Sessions"."IsCompleted" to boolean drop typemod,
     column "Sessions"."IsFailed"    to boolean drop typemod,
     column "Sessions"."IsCancelled" to boolean drop typemod,
     column "Sessions"."CreatedAtUtc"   to timestamptz using zero-dates-to-null,
     column "Sessions"."StartedAtUtc"   to timestamptz using zero-dates-to-null,
     column "Sessions"."CompletedAtUtc" to timestamptz using zero-dates-to-null,

     column "SessionTemplates"."CreatedAtUtc" to timestamptz using zero-dates-to-null,
     column "SessionTemplates"."UpdatedAtUtc" to timestamptz using zero-dates-to-null

EXCLUDING TABLE NAMES MATCHING 'ConductorHeartbeats', 'Logs'
;
PGLOADER

echo "==> Running pgloader..."
pgloader --client-min-messages warning "$LOAD_FILE"

echo ""
echo "==> Resetting PostgreSQL sequences..."
PGPASSWORD="$PG_PASS" psql -h "$PG_HOST" -p "$PG_PORT" -U "$PG_USER" -d "$PG_DB" <<SQL
SELECT setval(pg_get_serial_sequence('"Sessions"',        'Id'), COALESCE(MAX("Id"), 1)) FROM "Sessions";
SELECT setval(pg_get_serial_sequence('"Logs"',      'Id'), COALESCE(MAX("Id"), 1)) FROM "Logs";
SELECT setval(pg_get_serial_sequence('"SessionTemplates"','Id'), COALESCE(MAX("Id"), 1)) FROM "SessionTemplates";
SQL

echo ""
echo "==> Migration complete. Verify with:"
echo "    psql -h $PG_HOST -U $PG_USER -d $PG_DB -c 'SELECT COUNT(*) FROM \"Sessions\"; SELECT COUNT(*) FROM \"LogRecords\";'"
