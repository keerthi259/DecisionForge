# Phase 12 audit, outbox and notifications evidence

## Verification metadata

- Date (UTC): 2026-08-03T19:48:12Z
- Specification: `spec.md`, Phase 12, `DF-12-001` through `DF-12-010`
- Dependency reviewed: Phase 11 approval workflow, transaction contracts,
  concurrency tokens and audit-source events
- Toolchain: .NET SDK 10.0.302, Node.js 24.18.1, npm 11.16.0,
  Git 2.55.0 and Docker 29.5.3
- Containers: PostgreSQL 18.4 and Mailpit 1.30.5

## Atomic-task evidence

| Task | Evidence |
|---|---|
| `DF-12-001` | `AuditPayload` validates bounded flat fields, ordinally sorts them and emits deterministic UTF-8 JSON. Sensitive field-name tests reject credentials, tokens, policy definitions and uncontrolled reason text. Approval mappings were corrected to retain only note presence, length and SHA-256. |
| `DF-12-002` | `AuditEvent` assigns positive per-aggregate sequence, normalizes UTC time to PostgreSQL precision and calculates the specified SHA-256 chain. A fixed golden vector equals `21e0a346d234f14d6bfef415828dc3975c6c74936cac7ea7f0bae78b41487ecc`. |
| `DF-12-003` | `PostgresReliabilityStore.AppendAsync` receives the caller's open connection and transaction. A real PostgreSQL test writes representative business state plus audit/outbox and proves commit creates all three while rollback creates none. |
| `DF-12-004` | `AuditChainVerifier` detects payload/hash changes, previous-link changes and sequence gaps and returns the first invalid expected sequence. PostgreSQL reload also verifies. |
| `DF-12-005` | `ReliabilityEventMapper` has an explicit controlled mapping for all 31 currently defined domain-event shapes and creates matching audit/outbox envelopes. PostgreSQL inserts outbox rows in the same supplied transaction. |
| `DF-12-006` | `OutboxDispatcher` uses bounded batches, leases and capped exponential delay. Store attempt counts produce pending retry, visible terminal failure and final-lease-expiry failure. Unit and PostgreSQL tests cover retry/terminal behavior. |
| `DF-12-007` | Completion requires message ID plus current lease and a repeated completion returns false. Notification rows uniquely constrain source outbox identity and retain durable email-delivery state. Tests prove completed email, outbox and in-app notification delivery are not applied twice. |
| `DF-12-008` | Immutable `Notification`, strict notification payload handler and `INotificationSender` are implemented. The Mailpit HTTP adapter sends a deterministic delivery ID; a real Mailpit container returned the visible `Request approved` message. |
| `DF-12-009` | Completed-only, age- and batch-bounded PostgreSQL cleanup is implemented. The integration test deletes completed work and retains terminal-failed work; the query cannot select pending/failed rows. |
| `DF-12-010` | Thirteen infrastructure tests execute without skips, including PostgreSQL transaction, concurrent head locking, chain reload, lease/retry/cleanup, unique notification and real Mailpit delivery. |

## Phase-gate results

`./scripts/build.ps1` passed exact tool validation, locked restore, format
verification, Release build of all 14 backend projects, frontend formatting,
zero-warning ESLint, strict TypeScript checking and the Vite production build.
Backend output contained 0 warnings and 0 errors; npm reported 0
vulnerabilities.

`./scripts/test.ps1` passed 549 of 549 tests with no failures or skips:

- Domain unit: 359 (including one 100-case Phase 12 FsCheck property)
- Application unit: 122
- Infrastructure integration: 13
- API integration: 7
- Contract: 1
- Architecture: 45
- Performance-project tests: 2

Changed-project coverage gates passed:

```text
Domain: line 95.13%, branch 88.07%.
Application: line 93.84%, branch 88.31%.
Infrastructure: line 86.11%, branch 78.12%.
```

Coverage source artefacts:

- `.decisionforge/coverage/domain/20260803T194152352Z/bb9e711c-fced-4787-8914-ef1646028d11/coverage.cobertura.xml`
- `.decisionforge/coverage/application/20260803T194032822Z/fc6ed2ad-8113-4047-a030-d5bb6ec14025/coverage.cobertura.xml`
- `.decisionforge/coverage/infrastructure/20260803T194040923Z/4df61041-eee7-4f85-b3bb-b21d2d0bb2a8/coverage.cobertura.xml`

The NuGet transitive vulnerability scan reported no vulnerable package in all
14 projects. `npm audit --audit-level=high` reported 0 vulnerabilities. The
repository secret-signature scan reported 0 findings. The changed production
source scan reported no direct system clock/GUID call, blocking async call,
empty catch, TODO/FIXME, debug output or warning suppression.

Phase 12 changes no browser UI, API business contract, identity authorization,
policy evaluator algorithm or mutation target. Frontend E2E/accessibility,
policy mutation and new HTTP performance checks are therefore not applicable.
All existing API, contract and performance-project regression tests passed.

## Corrected gate findings

Docker Desktop was installed but its engine was initially stopped; it was
started and all required Testcontainers tests then executed successfully. The
first root build wrapper reached its 120-second timeout and was not counted as
a pass; the exact gate reran with a four-minute wrapper and passed in 158.3
seconds.

Early incremental compilation found one API misuse and analyzer/style findings;
all were corrected without suppression. The initial infrastructure coverage
measurement was 72.96% line / 32.25% branch. Additional worker, options,
transport-failure and registration behavior tests raised the verified result to
86.11% / 78.12%, above the required 75% / 65% gate.

The specification file retained SHA-256
`470B24BCDE6A9C676422F821B2FAF2959293A7D89A8EAD442FEE33F405E8FE68`.
