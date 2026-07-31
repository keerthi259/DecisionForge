# DecisionForge

DecisionForge is an explainable procurement decision and approval platform. Its
target workflow evaluates a purchase request against an immutable, versioned
policy, records rule-level evidence, and creates an ordered approval workflow
when manual review is required.

> Current state: Phase 3 provides the local Aspire topology, PostgreSQL and
> Mailpit resources, Vite-to-API proxy, telemetry defaults and operational
> health/version endpoints. No procurement workflow, EF model, authentication,
> policy evaluator, deployment, or product KPI is implemented.

The authoritative implementation specification is [spec.md](spec.md). It
defines the complete scope, architecture, security boundaries, quality gates,
and 24-phase Atomic Task Graph.

## Repository status

| Phase | Name | Status | Evidence |
|---:|---|---|---|
| 1 | Governance and repository bootstrap | Complete | `docs/evidence/phase-01-tool-validation.md` |
| 2 | Solution skeleton and static quality | Complete | `docs/evidence/phase-02-static-quality.md` |
| 3 | Local platform foundation | Complete | `docs/evidence/phase-03-local-platform.md` |
| 4 | Domain primitives and request aggregate | Not started | None |
| 5 | Reference data and evaluation facts | Not started | None |
| 6 | Policy JSON contract and validation | Not started | None |
| 7 | Deterministic policy engine | Not started | None |
| 8 | Policy lifecycle and versioning | Not started | None |
| 9 | Purchase-request application lifecycle | Not started | None |
| 10 | Decision orchestration and reproduction | Not started | None |
| 11 | Approval workflow | Not started | None |
| 12 | Audit, outbox and notifications | Not started | None |
| 13 | Identity and resource authorization | Not started | None |
| 14 | API foundation and cross-cutting behaviour | Not started | None |
| 15 | PostgreSQL persistence and business APIs | Not started | None |
| 16 | Simulation, dashboard and exports | Not started | None |
| 17 | Frontend foundation and authentication | Not started | None |
| 18 | Requester experience | Not started | None |
| 19 | Approver experience | Not started | None |
| 20 | Policy, audit and administration UI | Not started | None |
| 21 | Observability and operations | Not started | None |
| 22 | Quality, security and performance hardening | Not started | None |
| 23 | Containers, CI/CD and deployment | Not started | None |
| 24 | Documentation, demo and final release | Not started | None |

Detailed task status is maintained in
[docs/project/phase-status.md](docs/project/phase-status.md). Status is updated
to `Complete` only after every task and the phase gate pass.

## Prerequisites

- .NET SDK 10.0.302 (exactly pinned by `global.json`)
- Node.js 24.18.1 LTS (pinned by `.nvmrc` and `.node-version`)
- Git
- Docker Desktop or a compatible Docker Engine
- Bash for the Bash validation script, or Windows PowerShell 5.1+ for the
  PowerShell validation script

Check the workstation before development:

```powershell
./scripts/validate-tools.ps1
```

```bash
./scripts/validate-tools.sh
```

The scripts return a non-zero exit code and an actionable message when a
mandatory prerequisite is missing or does not match its pin.

## Architecture direction

DecisionForge will be a single-deployable modular monolith. Domain and
application layers remain independent of ASP.NET Core, EF Core and the UI; the
deterministic policy evaluator remains isolated from I/O; PostgreSQL is the one
business database; and the production ASP.NET Core host serves the React build
from the same origin. [ADR-0001](docs/adr/ADR-0001-modular-monolith.md) records
the initial architecture decision.

The current project layout and enforced references are documented in
[docs/architecture/component-view.md](docs/architecture/component-view.md).

## Build and test

After selecting the pinned tools, run the cross-platform baseline scripts:

```powershell
./scripts/build.ps1
./scripts/test.ps1
```

```bash
./scripts/build.sh
./scripts/test.sh
```

These commands use locked dependency restoration. The build script verifies
formatting, treats backend warnings as errors, and runs frontend formatting,
lint, strict type checking, and the Vite production build. See
[docs/testing/testing-strategy.md](docs/testing/testing-strategy.md) for the
current test scope and explicit future boundaries.

For one-command local startup and health verification, see
[docs/operations/local-development.md](docs/operations/local-development.md).

## Contributing and security

Read [CONTRIBUTING.md](CONTRIBUTING.md) before changing the repository. Report
security concerns using [SECURITY.md](SECURITY.md), never through a public issue.
Project conduct is governed by [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).

## Licence

DecisionForge is available under the [MIT License](LICENSE).
