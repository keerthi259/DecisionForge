# Phase 3 local platform evidence

## Verification metadata

- Date (UTC): 2026-07-31T21:50:31Z
- Specification: `spec.md`, Phase 3, `DF-03-001` through `DF-03-009`
- Dependency reviewed: Phase 2 (`42932cde4d334d91a1761a800f3ced7b023ff15a`)
- Toolchain: .NET SDK 10.0.302, Node.js 24.18.1, npm 11.16.0,
  Git 2.55.0 and Docker 29.5.3

## Atomic-task evidence

| Task | Evidence |
|---|---|
| `DF-03-001` | Aspire started pinned PostgreSQL 18.4 and Mailpit 1.30.5 resources before the API; the final smoke run reached all resources. |
| `DF-03-002` | Aspire started Vite on port 5173; `/health` and `/version` reached the API through the Vite proxy with no API CORS policy. |
| `DF-03-003` | ServiceDefaults registers OpenTelemetry logging, metrics and tracing, service discovery, standard HTTP resilience and health checks. |
| `DF-03-004` | Typed platform options validate on startup; an API integration test proves a missing application name reports its exact configuration path. |
| `DF-03-005` | `TimeProvider`, UUIDv7 generation and scoped correlation context are injectable; infrastructure tests prove deterministic IDs and nested context restoration. |
| `DF-03-006` | `.env.example` contains names and safe non-secret defaults only; local secret handling is documented. |
| `DF-03-007` | API tests prove a validated inbound correlation ID is shared by the response, logging scope and application accessor, and malformed input is replaced. |
| `DF-03-008` | Live Aspire checks returned liveness 200 and readiness 200; stopping only PostgreSQL changed readiness to 503 while liveness remained 200, and restart restored readiness to 200. |
| `DF-03-009` | PowerShell and Bash startup, smoke and teardown paths were executed. Teardown removed only recorded, image- and label-validated non-persistent containers and left zero Phase 3 containers running. |

## Static gate

`./scripts/build.ps1` passed with locked NuGet restoration, formatter
verification, a Release build of 14 projects with 0 warnings and 0 errors,
`npm ci`, Prettier, ESLint with zero warnings, strict TypeScript checking and a
Vite 8.2.0 production build. npm reported 0 vulnerabilities.

`./scripts/test.ps1` passed 19 of 19 tests with no skips across seven test
assemblies:

- Domain unit: 1
- Application unit: 1
- Infrastructure integration: 3
- API integration: 7
- Contract: 1
- Architecture: 5
- Performance-project structural baseline: 1

Both the PowerShell parser and `bash -n scripts/*.sh` accepted all lifecycle
scripts. `dotnet list DecisionForge.sln package --vulnerable
--include-transitive` and `npm audit --audit-level=high` reported no vulnerable
packages.

## Live phase gate

The final PowerShell lifecycle run produced:

```text
frontend: PASS (http://localhost:5173)
liveness: PASS (http://localhost:5066/health/live)
readiness: PASS (http://localhost:5066/health/ready)
version: PASS (1.0.0+42932cde4d334d91a1761a800f3ced7b023ff15a)
same-origin proxy and correlation: PASS
mailpit: PASS (http://localhost:8025)
```

The Bash smoke path produced the same six passes. The controlled database
outage and recovery produced:

```text
database-down readiness=503 liveness=200
database-recovered readiness=200
DecisionForge AppHost stopped.
phase3-running-container-count=0
```

Aspire reported that the current developer account does not trust its local
HTTPS certificate. The required HTTP topology and health gate were unaffected;
the optional trust command is documented in the local-development guide.

## Regression evidence

Verification exposed and corrected readiness test-host configuration timing,
strict TypeScript environment access, safe Aspire container teardown, Docker
inspection quoting, and WSL process ownership. Each affected automated check or
lifecycle path was rerun after correction. No acceptance failure remains.

Coverage, mutation score, accessibility automation and measured performance
budgets are assigned to later phases by the Atomic Task Graph and are not
claimed here.
