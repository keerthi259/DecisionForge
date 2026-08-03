# Security testing

## Phase 13 automated matrix

| Threat or misuse | Expected result | Automated evidence |
|---|---|---|
| Anonymous session read | 401, no redirect | `AuthenticationEndpointTests` |
| Missing or cross-session antiforgery token | Controlled 400 | `AuthenticationEndpointTests` |
| Invalid credentials | Uniform 401 without exception details | `AuthenticationEndpointTests` |
| Five failed passwords | Account lockout; correct password remains denied | `AuthenticationEndpointTests` |
| Login burst over configured window | 429 with retry information | `AuthenticationEndpointTests` |
| Requester reads another request | Authorization failure | `ResourceAuthorizationTests` |
| Unassigned/wrong approver reads request | Authorization failure | `ResourceAuthorizationTests` |
| Wrong role or completed stage action | Authorization failure | `ResourceAuthorizationTests` |
| Author publishes or publisher authors | Authorization failure | `ResourceAuthorizationTests` |
| Auditor mutates/admin manages audit | Authorization failure | `ResourceAuthorizationTests` |
| Administrator approves or overrides implicitly | Authorization failure | `ResourceAuthorizationTests` |
| Override without explicit permission | Authorization failure | `ResourceAuthorizationTests` |
| Existing non-demo alias collides with demo seed | Controlled failure; no role assignment | `IdentityPersistenceTests` |
| Demo setting enabled in Production | No demo user created | `IdentityPersistenceTests` |

The tests run against ASP.NET Core's real cookie, antiforgery, authorization,
Identity and rate-limiting components with PostgreSQL 18.4 Testcontainers.
## Phase 14 automated matrix

| Threat or misuse | Expected result | Automated evidence |
|---|---|---|
| Unknown/internal exception | Stable problem and trace; no diagnostic leak | `ApiFoundationIntegrationTests` |
| Unsupported sort/filter or page > 100 | Controlled field validation | `ApiFoundationIntegrationTests` |
| Missing, weak or stale ETag | 428, 400 or controlled 412 | `ApiFoundationIntegrationTests` |
| Same idempotency key and input | Original response replayed once | `ApiFoundationIntegrationTests` |
| Same key with different input | Controlled 409 | `ApiFoundationIntegrationTests` |
| Anonymous idempotency use | 401; no cross-user replay scope | `ApiFoundationIntegrationTests` |
| Body over 256 KiB configured limit | Controlled 413 | `ApiFoundationIntegrationTests` |
| Foreign Origin | No CORS authorization header | `ApiFoundationIntegrationTests` |
| Endpoint burst | 429 problem plus retry information | `ApiFoundationIntegrationTests` |
| Spreadsheet formula in CSV | Apostrophe-neutralized and correctly escaped | `SafeCsvTests` |
| Undocumented/breaking API change | Contract snapshot failure | `OpenApiContractSnapshotTests` |

Phase 15 applies these controls and Phase 13 resource handlers to business HTTP
endpoints. Phase 22 executes the complete release security matrix.
