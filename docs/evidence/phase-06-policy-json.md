# Phase 6 policy JSON contract evidence

## Verification metadata

- Date (UTC): 2026-08-01T00:23:19Z
- Specification: `spec.md`, Phase 6, `DF-06-001` through `DF-06-010`
- Dependency reviewed: Phase 5 and its Phase 1-4 foundations; direct baseline
  commit `4799747`
- Toolchain: .NET SDK 10.0.302, Node.js 24.18.1, npm 11.16.0,
  Git 2.55.0 and Docker 29.5.3

## Atomic-task evidence

| Task | Evidence |
|---|---|
| `DF-06-001` | Architecture tests prove schema, rule, outcome, condition and scalar-value contracts have no public constructors or setters. Parsed collections reject mutation. |
| `DF-06-002` | The sealed hierarchy contains exactly comparison, membership, existence, all, any and not nodes; structural tests reject empty, mixed and value-incompatible shapes. |
| `DF-06-003` | Registry and architecture tests prove exactly sixteen approved fact paths, with explicit value types, allowed operators and controlled values. |
| `DF-06-004` | Strict parsing rejects malformed JSON, comments, trailing commas, duplicate and unknown properties, wrong primitive/container types and unknown controlled names with safe errors. |
| `DF-06-005` | Semantic tests cover every approved fact and operator plus unknown facts, operator/type mismatches, duplicate IDs, invalid priorities, roles, schema versions and conflicting reasons. |
| `DF-06-006` | Exact boundary tests cover 256 KiB, 100 rules, depth 10, 25 logical children, 100 membership values and all required identifier/message lengths. |
| `DF-06-007` | Golden canonical text/checksum, reordered equivalent JSON, invariant decimal spelling and culture-independent round-trip tests pass. |
| `DF-06-008` | Invalid fixtures assert normalized path/code/severity/message errors and the absence of raw `JsonException` details. |
| `DF-06-009` | One complete valid fixture, five invalid fixtures and two 100-case FsCheck properties pass; focused policy coverage exceeds the 95%/90% gate. |
| `DF-06-010` | `policy-contract.md` and ADR-0002 document the exact `1.0` compatibility boundary, closed shapes, limits, security model and canonical form. |

## Phase-gate results

`./scripts/build.ps1` completed pinned-tool validation, locked restore, formatting
verification, a Release build of all 14 backend projects, and frontend format,
lint, strict typecheck and production build. Backend output contained 0 warnings
and 0 errors; npm reported 0 vulnerabilities.

`./scripts/test.ps1` passed 274 of 274 tests with no skips across seven test
assemblies:

- Domain unit: 218
- Application unit: 26
- Infrastructure integration: 3
- API integration: 7
- Contract: 1
- Architecture: 18
- Performance-project structural baseline: 1

The isolated Phase 6 property invocation passed 2 of 2 FsCheck properties, and
the isolated policy architecture invocation passed 4 of 4 tests. Changed
production code passed both applicable coverage gates:

```text
Domain coverage: line 96.52%, branch 93.43%.
Policy contract coverage: line 95.14%, branch 92.16%.
```

The NuGet transitive vulnerability scan reported no vulnerable packages in all
14 projects. The frontend build's npm audit reported 0 vulnerabilities.
The Bash policy-coverage gate also passed from WSL using the pinned Windows
executables, verifying parity with the PowerShell entry point.

Phase 6 changes no database mapping, API, browser, authentication,
authorization or runtime evaluation surface. PostgreSQL Testcontainers,
API-specific, Playwright, accessibility, mutation and measured performance
checks are therefore not applicable to this phase. Their existing structural
test projects still passed in the full suite.

## Engineering decisions and recovery evidence

ADR-0002 records the use of an explicit hand-written reader and closed AST
instead of general-purpose polymorphic deserialization, JSON Schema alone, or
dynamic expressions. Semantic compatibility is deny-by-default: only exact
schema version `1.0`, known properties, approved facts/operators and controlled
names are accepted. The checksum is a deterministic content identity, not a
publisher signature.

The first focused coverage run passed all tests but measured only 90.75% line
and 81.32% branch. Complete-node canonicalization and strict primitive-error
tests were added; the final focused gate measured 95.14% and 92.16%. An initial
new serializer test expected a one-role substring while the complete fixture
correctly contained two roles; the assertion was corrected. The first full
build then identified an IDE1006 constant-naming violation, which was corrected
without suppression. A subsequent build attempt exceeded the command wrapper
timeout and left only its spawned build processes; those exact processes were
stopped and the unchanged gate passed with a longer execution window.

Repository checks found no placeholders, warning suppressions, blocking async
calls, direct system-clock access in policy production code, secrets, debug
output or C# files over 300 lines. `git diff --check` passed. `spec.md` retained
SHA-256 `470B24BCDE6A9C676422F821B2FAF2959293A7D89A8EAD442FEE33F405E8FE68`.
