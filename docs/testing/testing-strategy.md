# Testing strategy

## Current baseline

The solution contains the complete backend test-project structure required by
`spec.md`: domain and application unit tests; infrastructure and API
integration tests; architecture and contract tests; a performance-test project;
and framework-neutral shared test utilities.

Phase 3 adds focused platform tests for:

- startup option validation and explicit configuration-path failures;
- injectable ID/time infrastructure and nested correlation scopes;
- safe correlation input plus response, logging and application propagation;
- liveness, readiness and version contracts;
- database-outage behavior; and
- the expanded AppHost/ServiceDefaults project graph.

The database-outage regression uses the real Npgsql health check against an
unreachable TCP endpoint. It verifies HTTP 503 readiness while liveness remains
HTTP 200. The Phase 3 smoke scripts separately verify the live Aspire-managed
PostgreSQL resource, Mailpit, API and Vite proxy.

Phase 4 adds domain tests covering all required value-object validation and
equality rules, controlled enum parsing, money arithmetic and storage
boundaries, item line totals, aggregate ownership, draft mutation atomicity,
server-authoritative totals, state-transition denial paths and exact domain
events. Test builders use only public factories and behaviour.

## Commands

Run the backend suite and static baseline:

```powershell
./scripts/test.ps1
./scripts/build.ps1
```

```bash
./scripts/test.sh
./scripts/build.sh
```

Run the local topology gate:

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

Run the Phase 4 domain coverage gate:

```powershell
./scripts/domain-coverage.ps1
```

```bash
./scripts/domain-coverage.sh
```

The current baseline is 111 tests across seven discovered assemblies. Domain
coverage is enforced at 90% line and 85% branch; the verified Phase 4 result is
96.18% line and 93.28% branch. PostgreSQL Testcontainers persistence tests,
Playwright, accessibility, mutation and measured performance budgets are
introduced and enforced by their assigned Atomic Task Graph phases. No metric
is claimed before it is measured.
