# Phase 14 API foundation evidence

## Verification metadata

- Date (UTC): 2026-08-03T21:19:57Z
- Specification: `spec.md`, Phase 14, `DF-14-001` through `DF-14-012`
- Dependency reviewed: Phase 13 Identity/resource authorization and all
  transitive predecessor phases
- Toolchain: .NET SDK 10.0.302, Node.js 24.18.1, npm 11.16.0,
  Git 2.55.0 and Docker 29.5.3
- Integration database: PostgreSQL 18.4 Testcontainers

## Atomic-task evidence

| Task | Evidence |
|---|---|
| `DF-14-001` | One route-group helper owns `/api/v1`; endpoint-data-source tests permit only that prefix or the three specified operational routes. |
| `DF-14-002` | Exception/status/rate/antiforgery paths return the explicit problem contract with stable error code and activity/HTTP trace ID. Unknown diagnostics are absent. |
| `DF-14-003` | Unsupported input returns 400 `validation.field`; controlled business violations return 422 `validation.business` with code/path/message entries. |
| `DF-14-004` | Query parser enforces offset >= 0, page size 1..100, one value per field and explicit per-endpoint sort/filter allow lists. |
| `DF-14-005` | Strong quoted GUID ETags round-trip; missing, weak/malformed and stale tokens return controlled 428, 400 and 412 responses. |
| `DF-14-006` | Opt-in middleware scopes key/fingerprint by stable endpoint and trusted user, replays the original bounded successful response and rejects changed input or anonymous scope. |
| `DF-14-007` | Host plus middleware body limits, deny-default exact-origin CORS and CSP/frame/MIME/referrer/permissions headers pass integration tests. |
| `DF-14-008` | Real endpoint rate limiting returns a common 429 problem and framework `Retry-After` metadata. |
| `DF-14-009` | OpenAPI 3.1 describes cookie authentication, protected operations, explicit problem responses and login/problem examples. |
| `DF-14-010` | Contract test deep-compares the generated document with `Snapshots/openapi-v1.json`, excluding only the host-specific `servers` value. |
| `DF-14-011` | CSV tests cover separators, quotes, CR/LF and spreadsheet formula prefixes, including whitespace-hidden formulas. |
| `DF-14-012` | Reusable API factory starts the real host and proves PostgreSQL server version 18.4; Identity tests share the same base fixture. |

## Phase-gate results

`./scripts/build.ps1` passed pinned tool validation, locked restore, format
verification, the Release build of all 14 backend projects, frontend clean
install, Prettier, zero-warning ESLint, strict TypeScript and Vite production
build. Backend output contained 0 warnings and 0 errors; npm reported 0
vulnerabilities.

`./scripts/test.ps1` passed 618 of 618 tests with no failures or skips:

- Domain unit: 359
- Application unit: 122
- Infrastructure integration: 13
- API integration: 67
- Contract: 2
- Architecture: 53
- Performance-project tests: 2

API coverage passed the 75% line / 65% branch threshold:

```text
API: line 87.46%, branch 68.09%.
```

Coverage artefact:

- `.decisionforge/coverage/phase14-api/20260803T211501933Z/8247dab0-a8b8-478c-b948-e225e7a10ed9/coverage.cobertura.xml`

The coverage command excludes only
`Microsoft.AspNetCore.OpenApi.SourceGenerators/**`. This is first-party
framework-generated XML-comment transformation code embedded in the API
assembly, not repository-authored behavior. The generated OpenAPI document is
still requested in API tests and deep-compared in full by the contract test.
No DecisionForge source is excluded.

NuGet reported no vulnerable direct or transitive package in all 14 projects.
`Microsoft.OpenApi` resolves to patched 2.11.0. `npm audit
--audit-level=high` reported 0 vulnerabilities. Secret-signature and Phase 14
guardrail scans reported 0 findings. `git diff --check` returned exit code 0.
No production schema creation/migration call or residual PostgreSQL test
container was present.

Phase 14 changes no frontend feature, policy evaluator or product HTTP
performance budget. Frontend component, Playwright, accessibility and mutation
checks are therefore not applicable. The existing authentication,
authorization, PostgreSQL, idempotency, concurrency and two affected
performance regressions all passed in the complete suite.

## Corrected gate findings

The first OpenAPI restore resolved vulnerable `Microsoft.OpenApi` 2.0.0 and
failed with `NU1903` for `GHSA-v5pm-xwqc-g5wc`. The dependency was not
suppressed; central transitive pinning now selects patched stable 2.11.0 and
the vulnerability scan is clear.

The first problem-response test found `WriteAsJsonAsync` replacing the intended
`application/problem+json` content type. The common writer now executes an
explicit JSON result and the media-type regression passes.

The first coverage run measured 78.68% line / 53.49% branch including
framework-generated OpenAPI helper code. Excluding only that generated source
still left authored branch coverage at 63.38%, so boundary tests were added for
body-limit and CORS option validation and ambiguous query allow lists. The
final authored result is 87.46% / 68.09%.

The specification file retained SHA-256
`470B24BCDE6A9C676422F821B2FAF2959293A7D89A8EAD442FEE33F405E8FE68`.
