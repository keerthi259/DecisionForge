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

Phase 8 adds policy aggregate and application tests for valid/invalid draft
retention, identity consistency, deterministic checksum refresh, monotonic
version allocation, stale concurrency, publication immutability, half-open UTC
range boundaries, overlap rejection, retirement, structured diffs,
cancellation and safe audit mappings. Architecture tests enforce closed
lifecycle entities and two specific non-generic cancellation-aware ports.
The verified Phase 8 results are 96.55%/92.67% for Domain, 96.46%/94.64%
for Application and 95.93%/91.60% for the affected policy contract.

Phase 9 adds request-domain regression tests for token rotation, stale writes,
atomic failures and independent cloning. Application tests cover trusted-user
ownership, create/update/item/withdraw/clone use cases, server totals,
non-disclosing resource denial, pagination boundaries, explicit projections,
submission precondition aggregation, cancellation and idempotent replay versus
conflicting fingerprints. Architecture tests enforce specific non-generic
ports, cancellation propagation, owner-scoped loading, immutable results and
the absence of ownership or total fields in mutating input contracts.

Phase 10 adds domain tests for checksum-valid evaluation sources, exact
half-open effective selection, zero/ambiguous policy failures, immutable
decision and rule evidence, final disposition transitions and retry-context
drift rejection. Application tests cover the flagship manual decision,
approved fact whitelisting, structured precondition failures, owner denial,
technical failure, cancellation, same-policy/same-input retry, exact replay,
conflicting fingerprints, explanation and historical reproduction with both
equivalent and drifted results. Architecture tests enforce specific
cancellation-aware ports, bounded service dependencies, owner-scoped decision
reads and the atomic request/decision/idempotency commit signature.
The verified Phase 10 results are 95.92% line / 90.02% branch for Domain and
96.25% line / 92.44% branch for Application. The complete suite passed 473
tests with no failures or skips.

Phase 11 adds domain tests for manual-only workflow creation, canonical role
ordering, single-stage activation, approve/reject progression, reason bounds,
wrong-role and invalid-state denial, stale/reused tokens, repeat actions,
terminal request transitions and override evidence. Application tests cover
trusted user/role resolution, explicit override permission, atomic
workflow/request commits, cancellation, bounded role-filtered inbox/detail
queries and safe resource denial. Architecture tests enforce specific
cancellation-aware ports, immutable workflow/projection surfaces, non-forgeable
actor/role commands, bounded pages and transaction signatures. Measured Phase
11 coverage and complete-suite counts are recorded in
`docs/evidence/phase-11-approval-workflow.md`.

Phase 12 adds domain golden-hash, canonical safe-payload, tamper, outbox-state
and notification tests; application mapping, retry, cancellation, idempotent
completion and notification-handler tests; and architecture boundary checks.
Infrastructure tests use pinned PostgreSQL 18.4 and Mailpit 1.30.5
Testcontainers with no conditional skip. They prove caller-owned transaction
commit/rollback, concurrent per-aggregate sequence serialization, hash reload,
lease/retry/terminal behavior, safe cleanup, unique in-app delivery and a
message visible through the real Mailpit API.

Phase 13 adds real PostgreSQL Identity persistence and API-host tests for
secure cookie options, idempotent role/demo seeding, production seed denial,
non-demo alias collision, login/me/logout, malformed credentials, cookie-bound
antiforgery, lockout and IP rate limiting. Authorization-service tests cover
request owner/assigned approver/auditor scope, draft mutation, pending-stage
role matching, author/publisher separation, read-only audit, administration and
explicit override permission. Architecture tests keep Identity/EF in
Infrastructure, HTTP context and handlers in API, and Domain/Application free
of those frameworks.

Phase 14 adds API integration tests for version routing, safe unknown/expected
problems, field versus business validation, pagination boundaries, unsupported
sort/filter names, strong/missing/malformed/stale ETags, authenticated
idempotency replay and changed-input conflict, anonymous replay denial, body
limits, security headers, restrictive CORS, endpoint 429 retry information and
CSV formula injection. A deep OpenAPI JSON snapshot detects contract drift.
The reusable API fixture and Identity fixture both run pinned PostgreSQL 18.4
through Testcontainers without conditional skips.

Phase 14 API coverage excludes only the first-party
`Microsoft.AspNetCore.OpenApi.SourceGenerators/**` XML-comment helper embedded
in the API assembly. No DecisionForge source is excluded; the generated
document is exercised and deep snapshot-compared. The measured API result is
87.46% line and 68.09% branch.

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
