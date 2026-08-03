# Phase 13 identity and authorization evidence

## Verification metadata

- Date (UTC): 2026-08-03T20:33:09Z
- Specification: `spec.md`, Phase 13, `DF-13-001` through `DF-13-010`
- Dependency reviewed: Phase 12 audit/outbox/notification boundary and all
  transitive predecessor phases
- Toolchain: .NET SDK 10.0.302, Node.js 24.18.1, npm 11.16.0,
  Git 2.55.0 and Docker 29.5.3
- Test database: PostgreSQL 18.4 Testcontainers

## Atomic-task evidence

| Task | Evidence |
|---|---|
| `DF-13-001` | `DecisionForgeIdentityDbContext` persists GUID Identity users, roles, claims and lockout state through EF Core/Npgsql. Tests verify the HTTP-only, Secure, SameSite Strict, essential, host-prefixed, path-rooted, non-sliding eight-hour application cookie and password/lockout settings. |
| `DF-13-002` | `IdentityRoleSeeder` creates all ten specified roles with application-generated IDs. Two reruns against PostgreSQL retain exactly ten unique roles. |
| `DF-13-003` | Demo aliases require an explicit setting, strong configured password and Development/Demo environment. Production creates zero users; reruns retain ten; a pre-existing non-demo alias fails without role escalation. No password exists in application settings. |
| `DF-13-004` | The no-store antiforgery endpoint issues a cookie-bound request token for `X-XSRF-TOKEN`. Login/logout reject missing or cross-session tokens with `authentication.antiforgery-invalid`. |
| `DF-13-005` | `HttpCurrentUserContext` accepts only an authenticated, non-empty GUID `NameIdentifier`. Reflection tests prove request/decision commands expose no requester input. |
| `DF-13-006` | The purchase-request handler passes owner read/draft mutation, assigned-approver read and auditor read while denying other requester, wrong approver, administrator, non-draft mutation and approver mutation. |
| `DF-13-007` | The approval-stage handler requires both a pending stage and the exact required role. Wrong role, completed stage and administrator are denied. |
| `DF-13-008` | Named author, publisher, audit and reference-admin policies are role-separated. Override requires the explicit `decision.override` permission; SeniorApprover role or Administrator alone is insufficient. |
| `DF-13-009` | Identity locks after five invalid passwords for 15 minutes; a correct password cannot bypass lockout. A separate IP-partitioned fixed-window limiter returns 429 plus retry information. |
| `DF-13-010` | Real API tests cover anonymous denial, malformed/invalid login, secure login cookie, `me`, logout, role persistence, all negative resource paths and safe error bodies. |

## Phase-gate results

`./scripts/build.ps1` passed pinned tool validation, locked restore, format
verification, Release build of all 14 backend projects, frontend formatting,
zero-warning ESLint, strict TypeScript checking and the Vite production build.
The backend reported 0 warnings and 0 errors; npm reported 0 vulnerabilities.

`./scripts/test.ps1` passed 577 of 577 tests with no failures or skips:

- Domain unit: 359
- Application unit: 122
- Infrastructure integration: 13
- API integration: 30
- Contract: 1
- Architecture: 50
- Performance-project tests: 2

Changed-project coverage passed the specification thresholds:

```text
API: line 95.45%, branch 72.30% (required 75% / 65%).
Infrastructure merged: line 96.68%, branch 81.52% (required 75% / 65%).
```

Coverage source artefacts:

- `.decisionforge/coverage/phase13-api/20260803T202930842Z/061d42cb-f263-46b8-9e4a-61d014797035/coverage.cobertura.xml`
- `.decisionforge/coverage/phase13-infrastructure-base/20260803T201425029Z/c8c6c621-a642-495f-ba44-54be24545826/coverage.cobertura.xml`
- `.decisionforge/coverage/phase13-infrastructure-merged-20260803T202930842Z.cobertura.xml`

The merged report was generated with Microsoft `dotnet-coverage` 18.8.0 from
the infrastructure-focused PostgreSQL suite and the API Identity/PostgreSQL
suite, so lines exercised by either required integration layer count once.

The NuGet transitive vulnerability scan reported no vulnerable package in all
14 projects. `npm audit --audit-level=high` reported 0 vulnerabilities. The
repository secret-signature scan reported 0 findings. The Phase 13 production
scan reported no direct clock/GUID call, blocking async call, async-void,
placeholder, debug output, warning suppression or runtime schema creation.
Browser source contains no local/session storage use.

Phase 13 changes no policy evaluator, frontend feature or HTTP performance
budget. Mutation, frontend component, Playwright and accessibility checks are
therefore not applicable. The existing property, contract, idempotency,
concurrency and performance regressions all passed in the complete suite.

## Corrected gate findings

The first prerequisite command resolved system Node.js 22 instead of the
pinned Node.js 24.18.1. Prepending the existing repository tool-cache paths
made both pins active and tool validation passed.

Initial compilation exposed that cookie configuration belonged in the API
boundary and that Infrastructure required the ASP.NET Core shared framework for
the supported `AddIdentity` registration. The references were corrected and
redundant package references removed without suppression.

The first antiforgery tests proved that middleware metadata alone did not
short-circuit these JSON minimal endpoints. An endpoint filter now consumes the
framework validation feature and returns a controlled failure. Regression tests
for missing and cross-session tokens both pass.

`dotnet-coverage` version 18.8.1 was not published; the available Microsoft
18.8.0 tool was installed only under the ignored evidence tool directory and
successfully produced the merged Cobertura report.

The specification file retained SHA-256
`470B24BCDE6A9C676422F821B2FAF2959293A7D89A8EAD442FEE33F405E8FE68`.
