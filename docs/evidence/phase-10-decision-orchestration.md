# Phase 10 decision orchestration and reproduction evidence

## Verification metadata

- Date (UTC): 2026-08-03T17:59:34Z
- Specification: `spec.md`, Phase 10, `DF-10-001` through `DF-10-009`
- Dependencies reviewed: Phases 8 and 9, plus evaluator/fact foundations from
  Phases 5-7
- Toolchain: .NET SDK 10.0.302, Node.js 24.18.1, npm 11.16.0,
  Git 2.55.0 and Docker 29.5.3

## Atomic-task evidence

| Task | Evidence |
|---|---|
| `DF-10-001` | `PolicyEvaluationSource` validates lifecycle state, UTC range and canonical checksum. `EffectivePolicySelector` independently applies the half-open range at submission time and returns stable zero/ambiguous errors. Five selector cases plus source-tampering coverage pass. |
| `DF-10-002` | `NormalizedEvaluationInputBuilder` maps the exact department/supplier projections already validated by the submission precondition service into domain sources. Tests assert the complete sixteen-path whitelist and absence of names/registration data. |
| `DF-10-003` | `Decision` is an immutable aggregate owning immutable `RuleEvaluation` entities and copied reasons/roles. It stores policy/version/checksum, normalized input, disposition, full condition traces and input/trace checksums. Domain and architecture immutability tests pass. |
| `DF-10-004` | `DecisionSubmissionService` performs owner/idempotency resolution, preconditions, timestamp selection, normalization, state transitions and evaluation. `IDecisionTransaction.CommitDecisionAsync` receives request, decision and idempotency record together; the lookup store has no independent write method. Architecture tests enforce the signature. |
| `DF-10-005` | Only non-domain, non-cancellation evaluator exceptions recover to `EvaluationFailed`; the safe error excludes exception text. Failure persistence creates no decision. Retry exact-loads the retained original policy identity/checksum and reuses the original normalized snapshot even when a newer candidate exists. |
| `DF-10-006` | `DecisionEvidenceService.GetExplanationAsync` owner-scopes the query using trusted user context and returns exact policy identity, normalized input, reasons, checksums and every rule trace. Foreign and missing resources share the same not-found result. |
| `DF-10-007` | Reproduction owner-loads the decision, exact-loads and verifies its historical policy version, evaluates stored facts and compares disposition/input/trace evidence. Equivalent, drifted, missing/tampered-policy and cancellation tests pass without mutating the original. |
| `DF-10-008` | The server hashes operation name, request ID and expected token. Matching key/fingerprint replays the original owner-scoped decision without request load or ID generation; changed input returns `purchase-request.idempotency-conflict` before request load. |
| `DF-10-009` | Fourteen new Domain, eighteen new Application and six new architecture cases cover the flagship manual decision, zero policy, bad references, technical failure, retry, replay/conflict, owner denial, cancellation, explanations and reproduction. |

## Phase-gate results

`./scripts/build.ps1` passed pinned-tool validation, locked restore, formatting,
Release build of all 14 backend projects, frontend formatting, zero-warning
lint, strict typecheck and production Vite build. Backend output contained 0
warnings and 0 errors; npm reported 0 vulnerabilities.

`./scripts/test.ps1` passed 473 of 473 tests with no failures or skips:

- Domain unit: 325
- Application unit: 99
- Infrastructure integration: 3
- API integration: 7
- Contract: 1
- Architecture: 36
- Performance-project tests: 2

Changed-project coverage gates passed:

```text
Domain: line 95.92%, branch 90.02%.
Application: line 96.25%, branch 92.44%.
```

Coverage source artefacts:

- `.decisionforge/coverage/domain/20260803T175048806Z/3923cf95-04cd-4071-8047-9cc947194d5b/coverage.cobertura.xml`
- `.decisionforge/coverage/application/20260803T175103344Z/1a9790a6-2af7-478a-b381-2a5a46b61de6/coverage.cobertura.xml`

The NuGet transitive vulnerability scan reported no vulnerable packages in all
14 projects, and `npm audit --audit-level=high` reported 0 vulnerabilities.
Phase 10 adds no EF mapping, API endpoint, browser UI, approval workflow,
PostgreSQL adapter or new evaluator algorithm. New PostgreSQL Testcontainers,
API, frontend, E2E, accessibility, mutation and performance checks are therefore
not applicable; all existing integration, API, contract and performance-project
regression suites still passed.

## Corrected gate findings

The initial shell resolved Node.js 22.14.0 and only .NET SDK 9.0.102. Exact
pinned .NET 10.0.302 and Node.js 24.18.1 distributions were installed under the
temporary system directory, outside the repository, and every recorded command
then passed `validate-tools.ps1`.

The first Application build identified a stateless builder method under
`CA1822`; it became a static cohesive builder and the coordinator dependency was
removed. No analyzer suppression was added. Repository scans found no
placeholders, warning suppressions, blocking async calls, direct domain clock or
GUID generation, debug statements, secrets or sensitive logging in the Phase 10
surface. `git diff --check` passed. `spec.md` retained SHA-256
`470B24BCDE6A9C676422F821B2FAF2959293A7D89A8EAD442FEE33F405E8FE68`.
The first generated-file scan wrapper read a stale native-command exit code
after a PowerShell pipeline and produced a false failure; the corrected
match-result predicate passed with no generated or sensitive path found.
