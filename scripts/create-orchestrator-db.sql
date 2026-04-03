-- PostgreSQL setup script for the Amass Conductor orchestrator database
-- Run as a superuser connected to the 'postgres' database:
--
--   psql -U postgres -v db_password="'your-password-here'" -f create-orchestrator-db.sql
--
-- The db_name and db_user default to 'orchestrator'; override with -v if needed:
--   psql -U postgres -v db_name=mydb -v db_user=myuser -v db_password="'s3cr3t'" -f create-orchestrator-db.sql

-- ── Defaults (override via -v on the command line) ─────────────────────────
\if :{?db_name}
\else
  \set db_name orchestrator
\endif

\if :{?db_user}
\else
  \set db_user orchestrator
\endif

-- db_password MUST be supplied via -v, e.g. -v db_password="'s3cr3t'"
\if :{?db_password}
\else
  \echo 'ERROR: db_password is required. Pass it with -v db_password="'"'"'your-password'"'"'"'
  \quit
\endif

-- ── Create role (idempotent) ───────────────────────────────────────────────
DO $role$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'db_user') THEN
        EXECUTE format('CREATE ROLE %I WITH LOGIN PASSWORD %L', :'db_user', :'db_password');
        RAISE NOTICE 'Role "%" created.', :'db_user';
    ELSE
        RAISE NOTICE 'Role "%" already exists, skipping create.', :'db_user';
    END IF;
END
$role$;

-- ── Create database (idempotent) ───────────────────────────────────────────
-- \gexec sends the query result as a SQL command; the SELECT returns nothing
-- when the database already exists, so no error is raised.
SELECT format('CREATE DATABASE %I OWNER %I', :'db_name', :'db_user')
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = :'db_name')
\gexec

\echo ''
\echo 'Connecting to database...'
\c :db_name

-- ── Schema privileges ──────────────────────────────────────────────────────
-- EF Core EnsureCreated needs CREATE + USAGE on public
GRANT CREATE ON SCHEMA public TO :db_user;
GRANT USAGE  ON SCHEMA public TO :db_user;

-- Existing objects (idempotent re-runs after a superuser ran migrations)
GRANT ALL PRIVILEGES ON ALL TABLES    IN SCHEMA public TO :db_user;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO :db_user;

-- Future objects created by any role in this schema
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT ALL PRIVILEGES ON TABLES    TO :db_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT ALL PRIVILEGES ON SEQUENCES TO :db_user;

\echo ''
\echo 'Setup complete.'
\echo 'Connection string (Npgsql key-value format):'
\echo '  Host=<host>;Port=5432;Database=:db_name;Username=:db_user;Password=<password>'
\echo ''
\echo 'URI format:'
\echo '  postgres://:db_user:<password>@<host>:5432/:db_name'
