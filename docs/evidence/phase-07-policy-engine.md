# Phase 7 deterministic policy engine evidence

## Verification metadata

- Date (UTC): 2026-08-01T01:25:35Z
- Specification: `spec.md`, Phase 7, `DF-07-001` through `DF-07-012`
- Dependency reviewed: Phase 6 and its Phase 1-5 foundations; direct baseline
  commit `4799747`
- Toolchain: .NET SDK 10.0.302, Node.js 24.18.1, npm 11.16.0,
  BenchmarkDotNet 0.15.8, dotnet-stryker 4.16.0, Git 2.55.0 and Docker 29.5.3

## Atomic-task evidence

| Task | Evidence |
|---|---|
| `DF-07-001` | A closed typed fact set admits only the sixteen registry paths and exact types. Immutable fact-access traces record path, type, existence and canonical value. Missing and unknown facts have controlled regression tests. |
| `DF-07-002` | Operator tests cover ordinal string and exact decimal/boolean equality, inequality, every numeric comparator and exact threshold boundaries. |
| `DF-07-003` | Tests and `policy-contract.md` define ordinal contains, duplicate-insensitive membership and presence-only exists/notExists semantics. |
| `DF-07-004` | Recursive all/any/not trees produce complete traces without short circuiting. Exact depth 10 passes and depth 11 is rejected by contract validation. |
| `DF-07-005` | Every rule produces a trace ordered by ascending priority then ordinal ID; source-rule and fact insertion permutations retain identical outputs and checksums. |
| `DF-07-006` | Rejected/manual/default precedence is covered by scenario tests and a 100-case FsCheck property. |
| `DF-07-007` | Reasons de-duplicate in contributing-rule order and roles de-duplicate in Department, Procurement, Security, Finance, Senior order. |
| `DF-07-008` | Result, reason, rule, condition and access contracts are immutable. Golden input checksum `8a0d654f3ea8cb6c22f5fece9f2a37587751ceac56780b7f6d9396a7a3a62fad` and trace checksum `4a9ed5a3cb090268efa434b1332ef652bc455363dba51b81693d10bf0abafeb3` are stable. |
| `DF-07-009` | Pre-cancellation propagates; cancellation is checked at every rule/node; exactly 2,500 condition evaluations pass and limit-plus-one fails with `policy.evaluation.execution-limit`. |
| `DF-07-010` | Golden low-value, flagship, suspended, restricted-cloud, threshold, emergency and missing-justification scenarios pass, plus three 100-case FsCheck properties. |
| `DF-07-011` | The 100-rule p95 is 2.470 ms against 50 ms. Stryker scores are 87.04% overall and 90.12% for critical operators/precedence. |
| `DF-07-012` | `policy-contract.md` and ADR-0002 match the implemented algorithm, ordering, canonicalization, limits, cancellation and safe failure behavior. |

## Phase-gate results

`./scripts/build.ps1` passed pinned-tool validation, locked restore, formatting,
the Release build of all 14 backend projects, frontend formatting, lint, strict
typecheck and production build. Backend output contained 0 warnings and 0
errors; npm reported 0 vulnerabilities.

`./scripts/test.ps1` passed 338 of 338 tests with no skips:

- Domain unit: 278
- Application unit: 26
- Infrastructure integration: 3
- API integration: 7
- Contract: 1
- Architecture: 21
- Performance: 2

The isolated Phase 7 property invocation passed 3 of 3 FsCheck properties and
the isolated evaluator architecture invocation passed 3 of 3 tests. Measured
coverage gates passed:

```text
Domain: line 96.49%, branch 92.83%.
Policy contract: line 95.59%, branch 91.62%.
Policy evaluator: line 96.38%, branch 90.00%.
```

The performance gate's independent 50-warmup/500-sample assertion measured a
2.470 ms p95. BenchmarkDotNet's 100-rule short job measured a 271.5 us mean,
13.52 us standard deviation and 223.85 KB allocated. The complete evaluation
namespace mutation run scored 87.04%; the explicit condition-operator and
outcome-precedence slice scored 90.12%.

NuGet's transitive vulnerability scan found no vulnerable packages in all 14
projects, and `npm audit` found 0 vulnerabilities. Phase 7 adds no database,
API, browser, authentication or authorization surface, so new PostgreSQL
Testcontainers, API-specific, end-to-end and accessibility tests are not
applicable. Existing integration and API assemblies still passed in the full
suite. Bash entry-point syntax checks passed for all three new phase scripts.

## Engineering decisions and recovery evidence

ADR-0002 extends the closed JSON decision to a pure synchronous interpreter.
The evaluator intentionally evaluates every logical child to provide a complete
trace, then applies fixed precedence and ordering in one aggregation step.
Partial fact sets are explicit typed inputs so missing-fact behavior is safe and
testable without making snapshot facts forgeable.

Initial production compilation found six analyzer violations; signatures and
concrete collection types were corrected without suppressions. Initial focused
coverage results (93.26%/83.59%, then 93.93%/89.09%) drove missing boundary and
safe-failure tests; the final result is 96.38%/90.00%. The first benchmark run
had a valid 1.362 ms p95 test but BenchmarkDotNet rejected a sealed benchmark
class; making only the benchmark declaring type non-sealed produced a valid
run. The first full mutation result passed overall at 83.94% but failed the
critical gate at 80.72%; exact depth, failure, duplicate-membership and reason
ordering tests plus removal of redundant cancellation branches raised the
official final scores to 87.04% and 90.12%. An initial full build invocation
also correctly rejected active Node v22.14.0; selecting the locally installed
pinned v24.18.1 runtime made the unchanged gate pass.

Repository checks found no placeholders, warning suppressions, blocking async
calls, direct system-clock access, secrets or debug output in Phase 7 code.
`git diff --check` and Bash syntax checks passed. `spec.md` retained SHA-256
`470B24BCDE6A9C676422F821B2FAF2959293A7D89A8EAD442FEE33F405E8FE68`.
