# Phase 4 domain evidence

## Verification metadata

- Date (UTC): 2026-07-31T22:32:01Z
- Specification: `spec.md`, Phase 4, `DF-04-001` through `DF-04-010`
- Dependencies reviewed: Phases 1 through 3; direct baseline commit `665f492`
- Toolchain: .NET SDK 10.0.302, Node.js 24.18.1, npm 11.16.0,
  Git 2.55.0 and Docker 29.5.3

## Atomic-task evidence

| Task | Evidence |
|---|---|
| `DF-04-001` | Identity-based `Entity`, `AggregateRoot`, `IDomainEvent` and stable domain-error primitives; architecture tests prove no outer-framework dependency. |
| `DF-04-002` | All thirteen required immutable value objects implemented; validation, normalization, equality, decimal precision, currency, arithmetic and overflow boundaries pass. |
| `DF-04-003` | All nine controlled enums implemented; exact-name parsing accepts every declared name and rejects null, casing variants, numeric and unknown values. |
| `DF-04-004` | `PurchaseRequestItem` validates ID, description, quantity, unit price and category and calculates bounded line totals. |
| `DF-04-005` | Caller-supplied IDs/time produce a deterministic, requester-owned Draft with zero authoritative total and one exact creation event. |
| `DF-04-006` | Aggregate-only item/metadata operations enforce Draft state and currency, recalculate totals and remain atomic on time, validation and overflow failures. |
| `DF-04-007` | Submission, withdrawal, evaluation start/failure/retry happy paths and denial matrix pass; submitted requests reject all editing. |
| `DF-04-008` | Tests assert exact creation, metadata, item, submission, evaluation and withdrawal event types, fields, order and timestamps. |
| `DF-04-009` | Deterministic builders use public behaviour only; architecture tests reject framework dependencies, mutable entity setters and test-only internal access. |
| `DF-04-010` | PowerShell and Bash coverage gates both report 96.18% line and 93.28% branch, exceeding 90% and 85%. |

## Phase gate results

`./scripts/build.ps1` completed locked restoration, formatting verification and
a Release build of all 14 projects with 0 warnings and 0 errors. The unchanged
frontend also passed clean install, Prettier, ESLint, strict TypeScript checking
and its Vite production build with 0 vulnerabilities.

`./scripts/test.ps1` passed 111 of 111 tests with no skips across seven test
assemblies:

- Domain unit: 88
- Application unit: 1
- Infrastructure integration: 3
- API integration: 7
- Contract: 1
- Architecture: 10
- Performance-project structural baseline: 1

The phase-specific architecture invocation passed 10 of 10 tests. Both
`./scripts/domain-coverage.ps1` and `bash scripts/domain-coverage.sh` passed the
coverage threshold:

```text
Domain coverage: line 96.18%, branch 93.28%.
Domain coverage gate passed.
```

PowerShell and Bash parser checks accepted every script. NuGet and npm audits
reported zero vulnerable packages. Repository scans found no warning
suppressions, placeholders, blocking async calls, direct domain system-clock or
GUID generation, debug output, secrets or oversized C# files. `spec.md` retained
SHA-256 `470B24BCDE6A9C676422F821B2FAF2959293A7D89A8EAD442FEE33F405E8FE68`.

## Defect and recovery evidence

Implementation verification exposed and corrected three defects:

1. `CurrencyCode` originally accepted a numeric character through the shared
   code validator. A regression case now requires exactly three ASCII letters.
2. Aggregate mutation timestamps and total overflow were initially checked
   after some in-memory changes. Validation and proposed totals are now checked
   before mutation, with regression tests proving state remains unchanged.
3. Read-only interfaces initially exposed list instances that could be
   downcast. True read-only wrappers now reject external item/event mutation.

An initial Bash coverage invocation selected unpinned WSL tools and failed tool
validation. The final invocation explicitly selected the repository-pinned
toolchain and passed. An initial full-build invocation exceeded the command
runner's 120-second limit; the same gate completed successfully with an extended
execution window.

Property-based tests, PostgreSQL domain mappings, mutation testing and measured
performance checks are assigned to later Atomic Task Graph phases and are not
claimed here.
