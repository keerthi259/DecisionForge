# Phase 5 reference-data and evaluation-fact evidence

## Verification metadata

- Date (UTC): 2026-07-31T23:19:14Z
- Specification: `spec.md`, Phase 5, `DF-05-001` through `DF-05-008`
- Dependency reviewed: Phase 4 and its Phase 1-3 foundations; direct baseline commit `dedf345`
- Toolchain: .NET SDK 10.0.302, Node.js 24.18.1, npm 11.16.0,
  Git 2.55.0 and Docker 29.5.3

## Atomic-task evidence

| Task | Evidence |
|---|---|
| `DF-05-001` | Department creation, zero/maximum threshold boundaries, identity/name/time validation, immutable code, detail concurrency and explicit deactivate/reactivate tests pass. |
| `DF-05-002` | Supplier registration/name validation, every declared approval/onboarding/risk value, invalid enum values, concurrency and activation tests pass. |
| `DF-05-003` | Separate department/supplier repository and active-query ports are cancellation-aware; architecture tests reject generic repositories. |
| `DF-05-004` | Management services use injected time/IDs, normalize through domain factories, reject duplicates/not-found/stale/repeated transitions with stable codes and skip no-op saves. |
| `DF-05-005` | Architecture tests prove the snapshot exposes exactly sixteen approved paths and no fact type has a public constructor or setter. |
| `DF-05-006` | Flagship 30-laptop facts, every category, urgency boundaries and mixed-category derivation pass deterministically. |
| `DF-05-007` | Creation, details-changed and activation-changed events for both aggregates use six controlled event types without names or free-form sensitive text. |
| `DF-05-008` | Tests reject empty requests, item-count overflow, past delivery, inactive references, mismatched IDs, currency mismatch, null input and invalid transitions. |

## Phase-gate results

`./scripts/build.ps1` completed pinned-tool validation, locked restore, formatting
verification, a Release build of all 14 backend projects, and frontend format,
lint, strict typecheck and production build. Backend output contained 0 warnings
and 0 errors; npm reported 0 vulnerabilities.

`./scripts/test.ps1` passed 174 of 174 tests with no skips across seven test
assemblies:

- Domain unit: 122
- Application unit: 26
- Infrastructure integration: 3
- API integration: 7
- Contract: 1
- Architecture: 14
- Performance-project structural baseline: 1

The phase-specific architecture invocation passed 14 of 14 tests. Changed
production projects passed their enforced coverage gates:

```text
Domain coverage: line 97.95%, branch 94.08%.
Application coverage: line 94.02%, branch 100.00%.
```

Phase 5 changes no persistence mapping, API, browser, authentication or
authorization surface. Therefore PostgreSQL Testcontainers, API-specific,
Playwright, accessibility, mutation and measured performance checks are not
applicable to this phase. Their existing structural test projects still passed
in the full suite.

## Engineering decisions and recovery evidence

Supplier onboarding is a separate controlled enum because the specification's
approved fact paths require an onboarding status and explicitly compare it with
`Suspended`; approval remains a separate state. Mixed-category requests expose
`Other` as their singular category fact and independently derive the
technology flag, avoiding order-dependent behavior. Caller-supplied tokens and
injected time preserve deterministic domain behavior.

Both PowerShell coverage gates passed. The new Application gate also passed
through Bash after selecting the pinned Windows executables from WSL, proving
the script's cross-platform path and command handling.

The initial gate invocation found that the pinned SDK and Node release were no
longer on PATH. Exact versions were restored in isolated user tool directories,
after which the repository's own validation scripts passed. Formatting
verification then detected static-field naming and import-order issues in new
tests; those were corrected and the gate reran successfully.

Repository scans found no warning suppressions, placeholders, blocking async
calls, direct domain system-clock/ID generation, debug output or oversized C#
files. `spec.md` retained SHA-256
`470B24BCDE6A9C676422F821B2FAF2959293A7D89A8EAD442FEE33F405E8FE68`.
