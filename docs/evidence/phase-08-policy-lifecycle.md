# Phase 8 policy lifecycle and versioning evidence

## Verification metadata

- Date (UTC): 2026-08-01T22:26:09Z
- Specification: `spec.md`, Phase 8, `DF-08-001` through `DF-08-009`
- Dependency reviewed: Phase 7 and its Phase 1-6 foundations
- Baseline commit: `8a1dc37b86e3417eba736e4495ab70e8028760c5`
- Toolchain: .NET SDK 10.0.302, Node.js 24.18.1, npm 11.16.0,
  Git 2.55.0 and Docker 29.5.3

## Atomic-task evidence

| Task | Evidence |
|---|---|
| `DF-08-001` | `Policy` is a sealed aggregate root owning a read-only non-empty `PolicyVersion` collection. Creation produces version 1 in Draft with normalized identity, raw definition text, validation state and controlled events. |
| `DF-08-002` | The aggregate allocates the previous maximum plus one while holding one application-managed concurrency token. Stale tokens and duplicate IDs fail without mutation; the future PostgreSQL uniqueness requirement is recorded in ADR-0007. |
| `DF-08-003` | Draft updates preserve submitted JSON exactly, recompute strict validation and assign canonical SHA-256 only when valid. Equivalent supported JSON produces the same checksum; identical text is a no-op. |
| `DF-08-004` | Publication requires a valid definition whose code/name match the aggregate. Invalid, malformed, backdated and invalid-range attempts fail atomically. |
| `DF-08-005` | Published and retired versions reject draft updates, republication and repeated retirement; public lifecycle collections and results reject mutation. |
| `DF-08-006` | Effective ranges use UTC `[from, until)` semantics. Tests cover adjacency, positive overlap, open ends, exact start/end, backdating, non-UTC values, range-closing retirement and preserving an earlier bounded end. |
| `DF-08-007` | Comparison returns ordered added/removed IDs, priority/condition/outcome flags per modified rule, and default-outcome changes; invalid versions cannot be compared. |
| `DF-08-008` | `IPolicyRepository` and `IPolicyQueries` are specific, non-generic and cancellation-aware. The concrete service implements create, create-draft, update, validate, publish, retire and compare use cases using injected time/IDs. |
| `DF-08-009` | Five controlled lifecycle event types and immutable safe audit mappings cover create, draft-create/update, publish and retire without full JSON or free text. Domain, application and architecture matrices pass. |

## Phase-gate results

`./scripts/build.ps1` passed pinned-tool validation, locked restore, formatting,
Release build of all 14 backend projects, frontend formatting, lint, strict
typecheck and production build. Backend output contained 0 warnings and 0
errors; npm reported 0 vulnerabilities.

`./scripts/test.ps1` passed 382 of 382 tests with no skips:

- Domain unit: 304
- Application unit: 40
- Infrastructure integration: 3
- API integration: 7
- Contract: 1
- Architecture: 25
- Performance-project tests: 2

The Phase 8 additions comprise 26 Domain lifecycle tests, 14 Application
policy/audit tests and 4 lifecycle architecture tests. Existing FsCheck,
integration, API, contract and performance-project regression suites also ran.
Changed-project and affected policy coverage gates passed:

```text
Domain: line 96.55%, branch 92.67%.
Application: line 96.46%, branch 94.64%.
Policy contract: line 95.93%, branch 91.60%.
```

The NuGet transitive vulnerability scan reported no vulnerable packages in all
14 projects, and `npm audit` reported 0 vulnerabilities. Phase 8 adds no EF,
database, API, browser, authentication or authorization surface; new
PostgreSQL Testcontainers, API-specific, E2E, accessibility, mutation and
performance checks are therefore not applicable. Their existing test projects
still passed where present in the full suite.

## Engineering decisions and recovery evidence

ADR-0007 records aggregate-owned optimistic concurrency, monotonic allocation,
published immutability, half-open effective ranges, historical retention and
the required Phase 15 database constraints. Invalid draft JSON is deliberately
retained without a definition/checksum. Audit mappings contain only controlled
IDs, version state, checksum and dates.

At the start of the phase, an external OneDrive state had removed every
root/source/Git-control file while leaving directories and build artifacts.
The remote URL was recovered from local PowerShell history, remote `main` was
verified at the reflog commit, and a clean temporary clone matched the approved
spec SHA-256 and Phase 7 evidence. The tracked tree and Git metadata were
restored without deleting surviving artifacts; damaged metadata remains in the
ignored `.decisionforge/recovery` directory. A first baseline test then failed
because untracked NuGet asset files were also absent; locked restore regenerated
them and the unchanged 278 Domain/26 Application baseline passed.

The first lifecycle architecture run found public `init` accessors on a
positional diff record; it was replaced by an internal constructor and get-only
properties, and the regression test passes. The first full build found private
constant naming violation IDE1006 in `PolicyCode`; the constant was corrected
without suppression and the complete build passed.

Repository checks found no placeholders, warning suppressions, blocking async
calls, direct system-clock access, secrets, sensitive logging or generated
artifacts in the tracked diff. `git diff --check` and formatting verification
passed. `spec.md` retained SHA-256
`470B24BCDE6A9C676422F821B2FAF2959293A7D89A8EAD442FEE33F405E8FE68`.
