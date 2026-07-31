# Phase 1 tool-validation evidence

## Metadata

- Date (UTC): 2026-07-31T19:05:47Z
- Phase/tasks: Phase 1; `DF-01-006`, `DF-01-007`, `DF-01-009`
- Environment: Windows PowerShell 5.1 host and WSL GNU Bash 5.3.9
- Commit: uncommitted
- Required pins: .NET SDK 10.0.302 and Node.js 24.18.1 LTS
- Version sources: Microsoft .NET 10 download metadata and the official Node.js
  24.18.1 security-release page, checked on 2026-07-31

The machine initially exposed .NET SDK 9.0.102 and Node.js v22.14.0. The pinned
toolchains were selected from a per-user cache after their official archive
checksums were verified. The cache is workstation state and is not part of the
repository.

## PowerShell validation

Command (the first two statements select the checksum-verified per-user tools):

```powershell
$env:DOTNET_ROOT = "$env:LOCALAPPDATA\DecisionForge\tools\dotnet-10.0.302"
$env:Path = "$env:DOTNET_ROOT;$env:LOCALAPPDATA\DecisionForge\tools\node-24.18.1\node-v24.18.1-win-x64;$env:Path"
./scripts/validate-tools.ps1
```

Result: PASS, exit code 0.

```text
dotnet: 10.0.302 (required 10.0.302)
node: v24.18.1 (required v24.18.1)
npm: 11.16.0
git: git version 2.55.0.windows.2
docker: Docker version 29.5.3, build d1c06ef
Tool validation passed.
```

## Bash validation

Command (PowerShell selects the same tools before launching WSL Bash):

```powershell
$env:Path = "$env:LOCALAPPDATA\DecisionForge\tools\dotnet-10.0.302;$env:LOCALAPPDATA\DecisionForge\tools\node-24.18.1\node-v24.18.1-win-x64;$env:Path"
bash -lc '/usr/bin/bash /mnt/c/Users/Gener/OneDrive/Documents/India_Interview/Decision_Forge/scripts/validate-tools.sh'
```

Result: PASS, exit code 0.

```text
dotnet: 10.0.302 (required 10.0.302)
node: v24.18.1 (required v24.18.1)
npm: 11.16.0
git: git version 2.55.0.windows.2
docker: Docker version 29.5.3, build d1c06ef
Tool validation passed.
```

## Failure-path checks

Both scripts were executed with restricted `PATH` values that omitted the
required toolchains. Each returned exit code 1, named every missing tool, and
reported the expected .NET and Node versions. This verifies that missing
prerequisites fail clearly rather than producing a false pass.

Parser checks also passed: the PowerShell parser reported 0 errors and
`bash -n scripts/validate-tools.sh` returned exit code 0. ShellCheck was not
installed and is not a Phase 1 gate requirement.
