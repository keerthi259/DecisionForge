# API foundation

Phase 14 establishes HTTP behavior in `DecisionForge.Api` without moving
business rules from Domain/Application or implementing Phase 15 persistence.

```mermaid
flowchart LR
  B[Browser or API client] --> C[Correlation]
  C --> E[Exception and status mapping]
  E --> H[Security headers and body limit]
  H --> O[Restrictive CORS]
  O --> A[Authentication and authorization]
  A --> R[Endpoint rate limiter]
  R --> F[Antiforgery]
  F --> I[Opt-in idempotency]
  I --> V[/api/v1 endpoint]
```

The common problem writer is used by exception, status, rate-limit,
antiforgery and authentication paths. Expected domain/application exceptions
map to controlled status/error codes; unknown exceptions are logged with only
the trace identifier and return a generic 500 contract.

Pagination parsing, ETag parsing/formatting and safe CSV encoding are stateless
API utilities. Idempotency is endpoint metadata plus middleware and a specific
store contract. It is intentionally opt-in because response capture is
appropriate only for bounded duplicate-sensitive actions. The store identity
is endpoint plus trusted user plus key; the request fingerprint prevents
changed-input replay. Phase 15 owns the durable store implementation and its
transaction integration.

First-party ASP.NET Core OpenAPI generates OpenAPI 3.1. A document transformer
adds title and secure-cookie authentication, an operation transformer marks
protected routes, and a schema transformer adds reviewed login/problem
examples. The contract test ignores only the host-specific `servers` value and
deep-compares every remaining JSON node with the committed snapshot.

The API test host exercises middleware without production-only test routes.
The reusable application factory starts pinned PostgreSQL 18.4 through
Testcontainers and is shared by Identity and Phase 14 API integration tests.
