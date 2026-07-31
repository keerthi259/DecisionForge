# Phase 2 static-quality evidence

## Metadata

- Date (UTC): 2026-07-31T20:33:02Z
- Phase/tasks: Phase 2; `DF-02-001` through `DF-02-009`
- Environment: Windows PowerShell 5.1 and WSL GNU Bash 5.3.9
- Toolchain: .NET SDK 10.0.302, Node.js 24.18.1, npm 11.16.0
- Commit state: evidence captured from the staged Phase 2 change set before its
  final repository commit.

## Locked dependencies

- `dotnet restore DecisionForge.sln --locked-mode`: PASS; 14 projects restored.
- `npm ci`: PASS; 161 packages installed from `package-lock.json`.
- NuGet locks: 14 `packages.lock.json` files.
- Central package check: no project-level `PackageReference` version attributes.
- `npm audit --audit-level=high`: PASS; 0 vulnerabilities.
- `dotnet list DecisionForge.sln package --vulnerable --include-transitive`:
  PASS; no vulnerable package was reported for any project.

## Static quality and build

- `dotnet format DecisionForge.sln --verify-no-changes --no-restore`: PASS.
- `dotnet build DecisionForge.sln --configuration Release --no-restore`: PASS;
  0 warnings and 0 errors across 14 projects.
- `npm run format:check`: PASS; all checked files match Prettier formatting.
- `npm run lint`: PASS; ESLint completed with zero warnings and errors.
- `npm run typecheck`: PASS; strict TypeScript project build completed.
- `npm run build`: PASS; Vite 8.2.0 transformed 17 modules and produced the
  production bundle in 953 ms on the recorded direct run.

## Test discovery and results

`dotnet test DecisionForge.sln --configuration Release --no-restore --no-build`
discovered all seven test assemblies. Total: 11 passed, 0 failed, 0 skipped.

| Project | Passed |
|---|---:|
| DecisionForge.Domain.UnitTests | 1 |
| DecisionForge.Application.UnitTests | 1 |
| DecisionForge.Infrastructure.IntegrationTests | 1 |
| DecisionForge.Api.IntegrationTests | 1 |
| DecisionForge.ArchitectureTests | 5 |
| DecisionForge.ContractTests | 1 |
| DecisionForge.PerformanceTests | 1 |

Integration and performance projects contain Phase 2 structural tests only.
They do not claim PostgreSQL integration or performance measurements before the
phases that implement those capabilities.

## Negative proof checks

### Warnings as errors

A temporary nullable dereference was added to the Domain project and this exact
command was executed:

```powershell
dotnet build src/DecisionForge.Domain/DecisionForge.Domain.csproj --configuration Release --no-restore
```

Expected result: FAIL, exit code 1. The compiler emitted `CS8602` as an error,
with `0 Warning(s)` and `1 Error(s)`. The temporary source file was removed and
the full solution then built with 0 warnings and 0 errors.

### Forbidden project reference

A temporary Domain-to-ServiceDefaults `ProjectReference` was added and the
production graph test was executed. Expected result: FAIL, exit code 1. The
failure reported that `DecisionForge.Domain` must not reference
`DecisionForge.ServiceDefaults`. After removing the temporary reference, all
five architecture tests passed.

The committed regression test also verifies that a synthetic forbidden Domain-
to-Infrastructure edge is rejected with the expected diagnostic.

## Cross-platform entry points

- `./scripts/build.ps1`: PASS; locked restore, format, 0-warning Release build,
  clean npm install, frontend format, lint, typecheck and build.
- `./scripts/test.ps1`: PASS; locked restore and all 11 tests.
- `/usr/bin/bash scripts/build.sh`: PASS with the same backend/frontend gates.
- `/usr/bin/bash scripts/test.sh`: PASS with all 11 tests.

## Corrected failures

- Initial solution evaluation exposed an incomplete per-user SDK extraction
  missing analyzer files. The checksum-verified SDK archive was re-extracted;
  no repository control was disabled.
- The first strict build rejected three implicit `var` declarations with
  `IDE0008`; explicit types were applied.
- The first frontend lint rejected a non-null assertion. Startup now checks the
  root element explicitly and throws a controlled initialization error when it
  is absent.

Coverage, mutation, accessibility and performance measurements are not Phase 2
gates and are not claimed by this evidence.
