# Atomic Task Graph status

Status values are `Not started`, `In progress`, `Complete`, `Partial`, or
`Blocked`. `Complete` requires every atomic task and the phase gate to pass with
recorded evidence. Future phases are not implied by directories or plans.

| Phase | Name | Dependencies | Status | Task range | Evidence |
|---:|---|---|---|---|---|
| 1 | Governance and repository bootstrap | None | Complete | DF-01-001..009 | `docs/evidence/phase-01-tool-validation.md` |
| 2 | Solution skeleton and static quality | 1 | Complete | DF-02-001..009 | `docs/evidence/phase-02-static-quality.md` |
| 3 | Local platform foundation | 2 | Complete | DF-03-001..009 | `docs/evidence/phase-03-local-platform.md` |
| 4 | Domain primitives and request aggregate | 3 | Complete | DF-04-001..010 | `docs/evidence/phase-04-domain.md` |
| 5 | Reference data and evaluation facts | 4 | Complete | DF-05-001..008 | `docs/evidence/phase-05-reference-data.md` |
| 6 | Policy JSON contract and validation | 5 | Complete | DF-06-001..010 | `docs/evidence/phase-06-policy-json.md` |
| 7 | Deterministic policy engine | 6 | Complete | DF-07-001..012 | `docs/evidence/phase-07-policy-engine.md` |
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

## Phase 3 task checklist

| Task | Status | Acceptance artefact |
|---|---|---|
| DF-03-001 | Complete | AppHost PostgreSQL, Mailpit and API resource graph |
| DF-03-002 | Complete | Aspire-managed Vite server and same-origin proxy smoke |
| DF-03-003 | Complete | ServiceDefaults OpenTelemetry, health, discovery and resilience |
| DF-03-004 | Complete | Validated `PlatformOptions` and negative startup test |
| DF-03-005 | Complete | Injectable `TimeProvider`, ID generator and correlation context |
| DF-03-006 | Complete | `.env.example` and local secret guidance |
| DF-03-007 | Complete | Correlation middleware, response header and logging-scope test |
| DF-03-008 | Complete | Live, ready and version endpoints plus outage regression test |
| DF-03-009 | Complete | PowerShell/Bash start, smoke and stop scripts |

## Phase 4 task checklist

| Task | Status | Acceptance artefact |
|---|---|---|
| DF-04-001 | Complete | Entity, aggregate root, domain event and stable domain-error primitives |
| DF-04-002 | Complete | Required immutable value objects and boundary/equality/arithmetic tests |
| DF-04-003 | Complete | Controlled enums and exact-name parser tests |
| DF-04-004 | Complete | `PurchaseRequestItem` and line-total boundary tests |
| DF-04-005 | Complete | Deterministic owned draft creation and creation event |
| DF-04-006 | Complete | Draft metadata/item mutations and server-authoritative totals |
| DF-04-007 | Complete | Submit, withdraw and evaluation-failure transition matrix |
| DF-04-008 | Complete | Exact significant-transition domain-event assertions |
| DF-04-009 | Complete | Public-API test builders and domain architecture tests |
| DF-04-010 | Complete | Enforced domain coverage: 96.18% line, 93.28% branch |

## Phase 5 task checklist

| Task | Status | Acceptance artefact |
|---|---|---|
| DF-05-001 | Complete | Department aggregate invariants and code/threshold/activation tests |
| DF-05-002 | Complete | Supplier aggregate invariants and registration/status/risk tests |
| DF-05-003 | Complete | Four specific, cancellation-aware repository/query ports; architecture guard |
| DF-05-004 | Complete | Department/supplier management commands, stable errors and orchestration tests |
| DF-05-005 | Complete | Non-forgeable immutable snapshot containing exactly sixteen approved fact paths |
| DF-05-006 | Complete | Deterministic technology and urgency derivation with golden/boundary tests |
| DF-05-007 | Complete | Six controlled reference-data domain-event types and payload assertions |
| DF-05-008 | Complete | Inactive, mismatched, currency, date, empty and overflow edge matrix |

## Phase 6 task checklist

| Task | Status | Acceptance artefact |
|---|---|---|
| DF-06-001 | Complete | Immutable schema, rule, condition, value and outcome contracts |
| DF-06-002 | Complete | Closed comparison, membership, existence, all, any and not AST with invalid-shape tests |
| DF-06-003 | Complete | Immutable metadata for exactly sixteen approved fact paths and allowed operators |
| DF-06-004 | Complete | Strict bounded parser with duplicate, unknown and malformed-input tests |
| DF-06-005 | Complete | Structural and semantic invalid-policy matrix |
| DF-06-006 | Complete | Exact size, count, depth, collection and text boundary tests |
| DF-06-007 | Complete | Fixed canonical serializer, invariant numbers and golden SHA-256 checksum |
| DF-06-008 | Complete | Safe normalized path/code/severity/message validation errors |
| DF-06-009 | Complete | Complete valid fixture, invalid fixtures and FsCheck properties |
| DF-06-010 | Complete | `policy-contract.md` and ADR-0002 schema-version policy |

## Phase 7 task checklist

| Task | Status | Acceptance artefact |
|---|---|---|
| DF-07-001 | Complete | Closed typed fact set and immutable access trace; unknown/missing fact tests |
| DF-07-002 | Complete | Exact equality and numeric operator boundary matrix |
| DF-07-003 | Complete | Membership, contains and existence semantics and tests |
| DF-07-004 | Complete | Recursive all/any/not evaluator and exact depth guard |
| DF-07-005 | Complete | Priority/ordinal rule ordering and complete per-rule trace |
| DF-07-006 | Complete | Rejected/manual/default precedence and FsCheck property |
| DF-07-007 | Complete | Fixed role ordering and deterministic reason de-duplication |
| DF-07-008 | Complete | Immutable result, canonical input checksum and full-trace checksum |
| DF-07-009 | Complete | Rule/node cancellation and bounded total condition evaluation |
| DF-07-010 | Complete | Golden scenario matrix and three 100-case FsCheck properties |
| DF-07-011 | Complete | 2.470 ms p95; 87.04% overall and 90.12% critical mutation scores |
| DF-07-012 | Complete | `policy-contract.md` algorithm and safe failure semantics |
