# DecisionForge

DecisionForge is an explainable procurement decision and approval platform. Its
target workflow evaluates a purchase request against an immutable, versioned
policy, records rule-level evidence, and creates an ordered approval workflow
when manual review is required.

> Current state: Phase 14 adds versioned API grouping, safe problem details,
> field/business validation mapping, bounded allow-listed list queries, strong
> ETags, authenticated idempotency middleware, body/CORS/header/rate controls,
> OpenAPI 3.1 contract snapshots and spreadsheet-safe CSV encoding. Business
> APIs and the Phase 15 reviewed production migration are not implemented.

The authoritative implementation specification is [spec.md](spec.md). It
defines the complete scope, architecture, security boundaries, quality gates,
and 24-phase Atomic Task Graph.

## Repository status

| Phase | Name | Status | Evidence |
|---:|---|---|---|
| 1 | Governance and repository bootstrap | Complete | `docs/evidence/phase-01-tool-validation.md` |
| 2 | Solution skeleton and static quality | Complete | `docs/evidence/phase-02-static-quality.md` |
| 3 | Local platform foundation | Complete | `docs/evidence/phase-03-local-platform.md` |
| 4 | Domain primitives and request aggregate | Complete | `docs/evidence/phase-04-domain.md` |
| 5 | Reference data and evaluation facts | Complete | `docs/evidence/phase-05-reference-data.md` |
| 6 | Policy JSON contract and validation | Complete | `docs/evidence/phase-06-policy-json.md` |
| 7 | Deterministic policy engine | Complete | `docs/evidence/phase-07-policy-engine.md` |
| 8 | Policy lifecycle and versioning | Complete | `docs/evidence/phase-08-policy-lifecycle.md` |
| 9 | Purchase-request application lifecycle | Complete | `docs/evidence/phase-09-request-lifecycle.md` |
| 10 | Decision orchestration and reproduction | Complete | `docs/evidence/phase-10-decision-orchestration.md` |
| 11 | Approval workflow | Complete | `docs/evidence/phase-11-approval-workflow.md` |
| 12 | Audit, outbox and notifications | Complete | `docs/evidence/phase-12-audit-outbox-notifications.md` |
| 13 | Identity and resource authorization | Complete | `docs/evidence/phase-13-identity-authorization.md` |
| 14 | API foundation and cross-cutting behaviour | Complete | `docs/evidence/phase-14-api-foundation.md` |
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
The implemented aggregate, value objects, invariants and state transitions are
documented in [docs/architecture/domain-model.md](docs/architecture/domain-model.md).
The supported policy shape, limits, schema-version policy and canonical form
are documented in
[docs/architecture/policy-contract.md](docs/architecture/policy-contract.md).
Policy lifecycle, effective-range and comparison behavior are documented in
[docs/architecture/policy-lifecycle.md](docs/architecture/policy-lifecycle.md).
The trusted request application boundary, pagination, cloning, preconditions
and idempotency semantics are documented in
[docs/architecture/purchase-request-application-lifecycle.md](docs/architecture/purchase-request-application-lifecycle.md).
Decision selection, atomic commit, retry, explanation and reproduction are
documented in
[docs/architecture/decision-orchestration.md](docs/architecture/decision-orchestration.md).
Approval ordering, authorization ports, action concurrency and override
semantics are documented in
[docs/architecture/approval-workflow.md](docs/architecture/approval-workflow.md).
Audit canonicalization, transactional outbox behavior and notification delivery
are documented in
[docs/architecture/audit-outbox-notifications.md](docs/architecture/audit-outbox-notifications.md).
Identity persistence, cookie/antiforgery boundaries and the resource policy
matrix are documented in
[docs/architecture/identity-and-authorization.md](docs/architecture/identity-and-authorization.md).

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
