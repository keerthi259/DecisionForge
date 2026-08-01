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

Phase 6 adds strict parser and semantic-validation matrices; every approved
fact and operator; all closed AST shapes; exact size, depth, count and text
limits; normalized-error abuse cases; valid and invalid JSON fixtures; a golden
canonical checksum; culture-independent round trips; and FsCheck properties
for canonicalization stability and malformed-input safety. Architecture tests
enforce immutable sealed policy contracts and the absence of executable-policy
technology dependencies.

Phase 7 adds typed fact access, every comparison/membership/existence operator,
recursive condition trees, exact depth and total-execution limits, complete
rule/access traces, precedence, ordered role/reason de-duplication, immutable
result/checksum golden scenarios, cancellation and three 100-case FsCheck
properties. BenchmarkDotNet exercises the maximum 100-rule policy, while
Stryker separately enforces the overall evaluator and critical
operator/precedence mutation thresholds.

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
./scripts/policy-coverage.ps1
./scripts/policy-engine-coverage.ps1
./scripts/policy-benchmark.ps1
./scripts/policy-mutation.ps1
```

```bash
./scripts/domain-coverage.sh
./scripts/application-coverage.sh
./scripts/policy-coverage.sh
./scripts/policy-engine-coverage.sh
./scripts/policy-benchmark.sh
./scripts/policy-mutation.sh
```

Domain coverage is enforced at 90% line and 85% branch; Application at 85%
line and 80% branch; and the Phase 6 policy namespace at 95% line and 90%
branch. The verified focused Phase 6 policy result is 95.14% line and 92.16%
branch. Phase 7 independently enforces 95% line and 90% branch for the evaluator,
75% overall evaluator mutation, 85% critical operator/precedence mutation and a
100-rule p95 latency below 50 milliseconds. The verified Phase 7 results are
96.38% line, 90.00% branch, 87.04% overall mutation, 90.12% critical mutation
and 2.470 ms p95. PostgreSQL Testcontainers reference
mappings, Playwright and accessibility remain assigned to later Atomic Task
Graph phases. No metric is claimed before it is measured.
