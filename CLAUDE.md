# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

API specification repository for the **Amass Engine Orchestrator** — an HTTP API for managing OWASP Amass Engine sessions and submitting Open Asset Model (OAM) assets. Licensed under Apache 2.0.

This repo currently contains only the OpenAPI/Swagger 2.0 spec; there is no application source code, build system, or tests.

## Repository Structure

- `docs/swagger.yaml` — OpenAPI 2.0 specification defining the Amass Engine API v1

## API Architecture

Base path: `/api/v1`

The API is session-based. Clients create an engine session with an Amass config, then interact with that session via its token (UUID).

**Endpoint groups (tags):**

| Tag | Purpose | Key endpoints |
|-----|---------|---------------|
| system | Health checks | `GET /health` |
| sessions | Session lifecycle | `POST /sessions`, `DELETE /sessions/{token}`, `GET /sessions/list`, `GET /sessions/{token}/stats` |
| assets | OAM asset ingestion | `POST /sessions/{token}/assets/{type}`, `POST /sessions/{token}/assets/{type}:bulk` |
| scope | Read scoped assets | `GET /sessions/{token}/scope/{type}` |
| ws | Real-time streaming | `GET /sessions/{token}/ws/logs` (WebSocket) |

**Supported OAM asset types:** `autonomous_system`, `fqdn`, `ipaddress`, `netblock`, `location`, `organization`

## Editing the Swagger Spec

When modifying `docs/swagger.yaml`:
- Follow OpenAPI/Swagger 2.0 conventions
- Session tokens are UUIDs passed as path parameters
- Request/response models are defined under `definitions:` and referenced via `$ref`
- The config model references `github_com_owasp-amass_amass_v5_config.Config` from the upstream Amass v5 project
