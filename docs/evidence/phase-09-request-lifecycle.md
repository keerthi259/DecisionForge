# Phase 9 purchase-request application lifecycle evidence

## Verification metadata

- Date (UTC): 2026-08-03T17:05:10Z
- Specification: `spec.md`, Phase 9, `DF-09-001` through `DF-09-008`
- Dependencies reviewed: Phase 5 directly and request foundations from Phases 1-4
- Toolchain: .NET SDK 10.0.302, Node.js 24.18.1, npm 11.16.0,
  Git 2.55.0 and Docker 29.5.3

## Atomic-task evidence

| Task | Evidence |
|---|---|
| `DF-09-001` | `IPurchaseRequestRepository`, `IPurchaseRequestQueries` and `IPurchaseRequestNumberGenerator` are specific, non-generic, requester-scoped and cancellation-aware. Architecture tests reject generic or cancellation-free ports. |
| `DF-09-002` | Create/update contracts contain no ownership input. `PurchaseRequestLifecycleService` obtains a non-empty user ID only from `ICurrentUserContext`; non-owned resources use the same not-found result as missing resources. |
| `DF-09-003` | Add/update/remove item contracts contain no total. The aggregate calculates line and request totals and rotates its concurrency token only after successful mutation. Invalid/stale operations remain atomic. |
| `DF-09-004` | List/detail operations pass the trusted requester to projection-only queries. Offset, the 1-100 page size, status and allow-listed sort are validated before I/O; result collections are defensive and read-only. |
| `DF-09-005` | Submission validation returns an immutable ordered error list for invalid state, missing items, past delivery, missing/inactive department or supplier and department-currency mismatch. It uses injected UTC time and cancellation-aware reference queries. |
| `DF-09-006` | Withdrawal enforces owner, state and expected token. Clone enforces owner/source token, creates a new Draft with a reserved request number and new aggregate/item/token identities, recalculates the total and leaves the source unchanged. |
| `DF-09-007` | The submission idempotency store is scoped by requester and key. Canonical lowercase SHA-256 fingerprints resolve to execute, replay the original request reference or a safe stable conflict. |
| `DF-09-008` | Seven new Domain cases, 41 new Application cases and five new architecture tests cover positive, negative, boundary, ownership, state, concurrency, cancellation and replay paths. |

## Phase-gate results

`./scripts/build.ps1` passed pinned-tool validation, locked restore, formatting,
Release build of all 14 backend projects, frontend formatting, zero-warning
lint, strict typecheck and production Vite build. Backend output contained 0
warnings and 0 errors; npm reported 0 vulnerabilities.

`./scripts/test.ps1` passed 435 of 435 tests with no failures or skips:

- Domain unit: 311
- Application unit: 81
- Infrastructure integration: 3
- API integration: 7
- Contract: 1
- Architecture: 30
- Performance-project tests: 2

Changed-project coverage gates passed:

```text
Domain: line 96.59%, branch 92.67%.
Application: line 95.64%, branch 93.47%.
```

Coverage source artefacts:

- `.decisionforge/coverage/domain/20260803T165759271Z/e26a9f06-6d6b-43bd-a7a4-f6d991c43e21/coverage.cobertura.xml`
- `.decisionforge/coverage/application/20260803T170714214Z/9e3b46e3-b4dd-42db-bcad-2125f7856da8/coverage.cobertura.xml`

The NuGet transitive vulnerability scan reported no vulnerable packages in all
14 projects, and `npm audit --audit-level=high` reported 0 vulnerabilities.
Phase 9 adds no EF mapping, API endpoint, authentication implementation,
browser UI, evaluator mutation target or performance budget; new PostgreSQL,
API, frontend, E2E, accessibility, mutation and performance tests are therefore
not applicable. Existing integration, API, contract and performance-project
regression suites still passed.

## Corrected gate findings

The first focused architecture run found public `init` accessors on positional
query projection records. They were replaced by public constructors and
get-only properties, and the five-test request architecture matrix passed.

The first exact build found two private test constants that violated IDE1006;
they were renamed without suppression. A later build attempt encountered a
transient OneDrive `EBUSY` lock during `npm ci`; the unchanged clean install
then passed, followed by a successful complete build gate.

Repository scans found no placeholders, warning suppressions, blocking async
calls, direct system-clock or GUID generation in Domain/Application, debug
statements, secrets or sensitive logging in the Phase 9 surface. `git diff
--check` passed. `spec.md` retained SHA-256
`470B24BCDE6A9C676422F821B2FAF2959293A7D89A8EAD442FEE33F405E8FE68`.
