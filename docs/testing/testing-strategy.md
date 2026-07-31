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

Phase 5 adds department and supplier invariant tests; application orchestration
tests using hand-written specific ports; golden fact-snapshot and derived-fact
tests; inactive, mismatched and boundary cases; cancellation tests; and
architecture checks for fact-path and repository constraints.

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

Run the changed-project coverage gates:

```powershell
./scripts/domain-coverage.ps1
./scripts/application-coverage.ps1
```

```bash
./scripts/domain-coverage.sh
./scripts/application-coverage.sh
```

The current baseline is 174 tests across seven discovered assemblies, with no
skips. Domain coverage is enforced at 90% line and 85% branch; Application at
85% line and 80% branch. The verified Phase 5 results are 97.95%/94.08% for
Domain and 94.02%/100.00% for Application. PostgreSQL Testcontainers reference
mappings, Playwright, accessibility, mutation and measured performance budgets
are introduced and enforced by their assigned Atomic Task Graph phases. No
metric is claimed before it is measured.
