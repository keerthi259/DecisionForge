# Local development topology

Phase 3 runs DecisionForge through one Aspire AppHost. The local topology is:

```text
PostgreSQL 18.4 ─┐
                 ├─> API ─> Vite/React
Mailpit 1.30.5 ──┘
```

Aspire generates the PostgreSQL username and password, injects the
`decisionforge` connection string into the API, and waits for PostgreSQL and
Mailpit health before starting the API. Vite starts only after the API is
healthy. No credentials are stored in the repository.

## Prerequisites

Select the repository-pinned .NET and Node toolchains and start Docker Desktop.
Then use either platform entry point:

```powershell
./scripts/start-local.ps1
./scripts/smoke-local.ps1
./scripts/stop-local.ps1
```

```bash
./scripts/start-local.sh
./scripts/smoke-local.sh
./scripts/stop-local.sh
```

The start command performs locked restoration, a Release build and `npm ci`,
starts Aspire in the background, and runs the smoke checks. Runtime state and
logs are written under the ignored `.decisionforge/` directory. The stop
command validates that the recorded PID belongs to this AppHost before sending
a termination signal. It also removes only the two container IDs recorded as
newly created by that launch, after revalidating their pinned image names and
Aspire non-persistent labels.

Under WSL with Windows-hosted .NET and Docker, the Bash wrappers delegate
process creation and teardown to their PowerShell counterparts so the recorded
PID is the actual Windows AppHost rather than a short-lived WSL interop process.
Native Unix environments use the Bash implementation directly.

Aspire may report that its local HTTPS development certificate is not trusted.
The Phase 3 endpoints use HTTP, so this does not affect the local gate. To use
the Aspire dashboard over trusted HTTPS, run `dotnet dev-certs https --trust`
for the current developer account.

Default local URLs:

- Frontend: `http://localhost:5173`
- API: `http://localhost:5066`
- Liveness: `http://localhost:5066/health/live`
- Readiness: `http://localhost:5066/health/ready`
- Version: `http://localhost:5066/version`
- Mailpit: `http://localhost:8025`

Vite proxies `/api`, `/health` and `/version` to the API. Browser calls remain
same-origin; the API does not enable permissive CORS.

## Configuration and secrets

`.env.example` documents non-secret environment names but is not a credential
file. Use .NET user-secrets or process environment variables for developer
secrets. Never commit a populated `.env`, PostgreSQL connection string, demo
password, cookie or API key. Demo identities and their password configuration
are intentionally deferred to Phase 13.

The API validates `DecisionForge:Platform` during startup. Missing application
or correlation-header settings stop startup with the exact configuration path.
The database connection is supplied by Aspire rather than `appsettings.json`.

## Health semantics

`/health/live` contains process-only checks. `/health/ready` includes the real
Npgsql connectivity check. A database outage therefore makes readiness return
HTTP 503 while liveness remains HTTP 200. Health requests are excluded from
ASP.NET request tracing so probes do not create normal request spans.
