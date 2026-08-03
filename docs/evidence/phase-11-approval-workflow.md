# Phase 11 approval workflow evidence

## Verification metadata

- Date (UTC): 2026-08-03T18:43:19Z
- Specification: `spec.md`, Phase 11, `DF-11-001` through `DF-11-009`
- Dependency reviewed: Phase 10, including the decision transaction,
  immutable decision evidence and request `PendingApproval` transition
- Toolchain: .NET SDK 10.0.302, Node.js 24.18.1, npm 11.16.0,
  Git 2.55.0 and Docker 29.5.3

## Atomic-task evidence

| Task | Evidence |
|---|---|
| `DF-11-001` | `ApprovalWorkflow.Create` rejects non-manual decisions, owns immutable stages and is supplied to the decision transaction only for `ManualApprovalRequired`. Auto-approved and rejected submission tests assert a null workflow. |
| `DF-11-002` | `PolicyApproverRoleOrder` is shared by policy outcome aggregation and `ApprovalStagePlanBuilder`; the five-role order, duplicate collapse, empty plan and unknown role are tested. |
| `DF-11-003` | Creation and progression tests prove exactly one pending stage while active. Approval activates only the next stage and rotates the token observed while it was waiting. Completion leaves no pending stage. |
| `DF-11-004` | Domain actions enforce pending state, exact role, actor, bounded note and mandatory rejection reason. Application actions obtain actor/roles only from trusted context/authorization ports. Wrong-role, waiting-stage, malformed and cancellation paths are tested. |
| `DF-11-005` | Final approval and every rejection call `PurchaseRequest.CompleteApproval`; `IApprovalActionTransaction.CommitAsync` receives workflow and request together. Approved/rejected request states and domain events are asserted. |
| `DF-11-006` | Acted and activated stage tokens rotate. Stale, reused, repeated and completed actions return stable conflict/not-actionable errors and do not commit duplicate outcomes. Prevalidation regression tests prove invalid actor or activation token cannot partially mutate stages. |
| `DF-11-007` | `IApprovalQueries` exposes only bounded explicit projections. The query service passes trusted user/role/override scope, validates max page size 100, rejects unauthorized filters and rechecks detail role scope. |
| `DF-11-008` | `ApprovalWorkflowService.OverrideAsync` requires explicit `CanOverrideDecision` permission, a reason and fresh current-stage token. The immutable original `ManualApprovalRequired` disposition remains, while actor/outcome/reason/time are recorded in override evidence, a dedicated domain event and controlled audit mapping. |
| `DF-11-009` | Domain, application and architecture tests cover creation, progression, terminal outcomes, authorization, queries, override, cancellation, stale/repeat actions, immutable contracts and transaction boundaries. `docs/architecture/approval-workflow.md` documents the implemented behavior and later-phase boundaries. |

## Phase-gate results

`./scripts/build.ps1` passed exact tool validation, locked restore, format
verification, Release build of all 14 backend projects, frontend formatting,
zero-warning ESLint, strict TypeScript checking and the Vite production build.
Backend output contained 0 warnings and 0 errors; npm reported 0 vulnerabilities.

`./scripts/test.ps1` passed 508 of 508 tests with no failures or skips:

- Domain unit: 340
- Application unit: 114
- Infrastructure integration: 3
- API integration: 7
- Contract: 1
- Architecture: 41
- Performance-project tests: 2

Changed-project coverage gates passed:

```text
Domain: line 95.40%, branch 89.38%.
Application: line 94.87%, branch 89.45%.
```

Coverage source artefacts:

- `.decisionforge/coverage/domain/20260803T183943343Z/de1ecdd1-cb98-42f0-9835-9d2ef387e586/coverage.cobertura.xml`
- `.decisionforge/coverage/application/20260803T183951516Z/6d7d649a-6c8d-4eaa-bbfd-9468656c582c/coverage.cobertura.xml`

The NuGet transitive vulnerability scan reported no vulnerable packages in all
14 projects. `npm audit --audit-level=high` reported 0 vulnerabilities. The
source review found zero direct system-clock/GUID calls, blocking async calls,
empty catches or logging calls in the approval production surface. The
repository signature scan found no private key, AWS, GitHub or Google API key
pattern, and generated build/test paths did not pollute `git status`.

Phase 11 adds no EF Core mapping, PostgreSQL adapter, API endpoint, browser UI
or new evaluator algorithm. New PostgreSQL Testcontainers, API security,
frontend, E2E, accessibility, mutation and HTTP performance checks are not
applicable to this phase. All existing integration, API, contract and
performance-project regression suites still passed.

## Corrected gate findings

The first baseline `scripts/build.ps1` invocation exceeded its 120-second
wrapper timeout; the same exact gate subsequently completed twice, with the
final run passing in 156.5 seconds. The first incremental build identified
`CA1859` on the role-order field and was corrected without suppression. Early
test compilation found style-analyzer issues and a duplicate-role test fixture;
both were corrected before the green suite.

Final manual review found two possible in-memory partial-mutation paths if a
domain caller supplied an empty actor ID or reused a waiting-stage token for
activation. Validation now occurs before any mutation, with regression tests.
The 331-line workflow file was also split to a focused guard, leaving the main
aggregate file at 270 lines. `git diff --check` and final format verification
passed. No warning suppression, placeholder or debug statement was added.
`spec.md` retained SHA-256
`470B24BCDE6A9C676422F821B2FAF2959293A7D89A8EAD442FEE33F405E8FE68`.
