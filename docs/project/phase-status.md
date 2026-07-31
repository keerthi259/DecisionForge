# Atomic Task Graph status

Status values are `Not started`, `In progress`, `Complete`, `Partial`, or
`Blocked`. `Complete` requires every atomic task and the phase gate to pass with
recorded evidence. Future phases are not implied by directories or plans.

| Phase | Name | Dependencies | Status | Task range | Evidence |
|---:|---|---|---|---|---|
| 1 | Governance and repository bootstrap | None | Complete | DF-01-001..009 | `docs/evidence/phase-01-tool-validation.md` |
| 2 | Solution skeleton and static quality | 1 | Complete | DF-02-001..009 | `docs/evidence/phase-02-static-quality.md` |
| 3 | Local platform foundation | 2 | Not started | DF-03-001..009 | None |
| 4 | Domain primitives and request aggregate | 3 | Not started | DF-04-001..010 | None |
| 5 | Reference data and evaluation facts | 4 | Not started | DF-05-001..008 | None |
| 6 | Policy JSON contract and validation | 5 | Not started | DF-06-001..010 | None |
| 7 | Deterministic policy engine | 6 | Not started | DF-07-001..012 | None |
| 8 | Policy lifecycle and versioning | 7 | Not started | DF-08-001..009 | None |
| 9 | Purchase-request application lifecycle | 5 | Not started | DF-09-001..008 | None |
| 10 | Decision orchestration and reproduction | 8, 9 | Not started | DF-10-001..009 | None |
| 11 | Approval workflow | 10 | Not started | DF-11-001..009 | None |
| 12 | Audit, outbox and notifications | 11 | Not started | DF-12-001..010 | None |
| 13 | Identity and resource authorization | 12 | Not started | DF-13-001..010 | None |
| 14 | API foundation and cross-cutting behaviour | 13 | Not started | DF-14-001..012 | None |
| 15 | PostgreSQL persistence and business APIs | 14 | Not started | DF-15-001..012 | None |
| 16 | Simulation, dashboard and exports | 15 | Not started | DF-16-001..010 | None |
| 17 | Frontend foundation and authentication | 16 | Not started | DF-17-001..010 | None |
| 18 | Requester experience | 17 | Not started | DF-18-001..010 | None |
| 19 | Approver experience | 18 | Not started | DF-19-001..009 | None |
| 20 | Policy, audit and administration UI | 19 | Not started | DF-20-001..010 | None |
| 21 | Observability and operations | 20 | Not started | DF-21-001..011 | None |
| 22 | Quality, security and performance hardening | 21 | Not started | DF-22-001..013 | None |
| 23 | Containers, CI/CD and deployment | 22 | Not started | DF-23-001..014 | None |
| 24 | Documentation, demo and final release | 23 | Not started | DF-24-001..012 | None |

## Phase 1 task checklist

| Task | Status | Acceptance artefact |
|---|---|---|
| DF-01-001 | Complete | `spec.md`; README specification link |
| DF-01-002 | Complete | `.gitignore`; `.gitattributes`; clean status gate |
| DF-01-003 | Complete | `LICENSE`; `SECURITY.md`; `CONTRIBUTING.md`; `CODE_OF_CONDUCT.md` |
| DF-01-004 | Complete | Honest `README.md` status table |
| DF-01-005 | Complete | ADR template and accepted ADR-0001 |
| DF-01-006 | Complete | `global.json`; `.nvmrc`; `.node-version` |
| DF-01-007 | Complete | Windows and Bash validation scripts |
| DF-01-008 | Complete | Evidence policy and this phase tracker |
| DF-01-009 | Complete | Executed tool-validation evidence |

## Phase 2 task checklist

| Task | Status | Acceptance artefact |
|---|---|---|
| DF-02-001 | Complete | `DecisionForge.sln`; six backend production projects |
| DF-02-002 | Complete | Project references and architecture tests |
| DF-02-003 | Complete | Eight test/support projects; backend test discovery |
| DF-02-004 | Complete | `src/DecisionForge.Web`; production build |
| DF-02-005 | Complete | `Directory.Build.props`; deliberate-warning proof |
| DF-02-006 | Complete | `Directory.Packages.props`; NuGet and npm lock files |
| DF-02-007 | Complete | `.editorconfig`; TypeScript, ESLint, and Prettier configuration |
| DF-02-008 | Complete | Architecture policy and forbidden-reference proof |
| DF-02-009 | Complete | PowerShell and Bash root build/test scripts |
