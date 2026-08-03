# API guide

## Version and formats

Application routes are grouped under `/api/v1`. The specified operational
routes `/health/live`, `/health/ready` and `/version` remain unversioned. JSON
uses camel case and UTC ISO-8601 timestamps. OpenAPI 3.1 is available at
`/api/v1/openapi/v1.json` and is protected by a reviewed contract snapshot.

## Authentication and browser mutations

The API uses the HTTP-only `__Host-DecisionForge-Auth` cookie documented in
`authentication.md`. OpenAPI describes this cookie as `cookieAuth` and marks
protected operations. Browser mutations additionally send the cookie-bound
antiforgery request token in `X-XSRF-TOKEN`.

## Problem contract

Every controlled API failure uses `application/problem+json`:

```json
{
  "type": "https://decisionforge.local/problems/validation.field",
  "title": "The request contains invalid fields.",
  "status": 400,
  "errorCode": "validation.field",
  "traceId": "00-example-trace-00",
  "instance": "/api/v1/example",
  "errors": [
    {
      "code": "query.sort.unsupported",
      "path": "sort",
      "message": "The sort field is not supported."
    }
  ]
}
```

Field/shape failures use `validation.field` and HTTP 400. Business validation
uses `validation.business` and HTTP 422. Missing resources use 404, invalid
state or duplicate/idempotency conflicts use 409, missing `If-Match` uses 428,
and stale concurrency uses 412. Unexpected exceptions return
`internal.error` without exception messages or stack traces.

## Pagination, sorting and filtering

List contracts use zero-based `offset` and bounded `pageSize`. The default is
25 and the maximum is 100. `sort=field` is ascending and `sort=-field` is
descending. Each endpoint supplies explicit sort/filter allow lists;
unsupported names, duplicate values and malformed boundaries return field
validation errors. No endpoint may pass arbitrary field names into a query.

## ETags

Concurrency-protected reads emit one strong quoted ETag derived from the
application-managed GUID concurrency token. Mutations require the exact ETag
in `If-Match`. Wildcards, weak ETags, multiple values and malformed tokens are
rejected. A missing precondition returns 428 and a stale token returns 412 with
`concurrency.conflict`.

## Idempotency

An endpoint opting into idempotency must have a stable endpoint name and an
authenticated GUID user. It requires one visible-ASCII `Idempotency-Key` of at
most 128 characters. The fingerprint covers HTTP method, route, query, content
type and body. Scope includes endpoint name and trusted user ID.

The middleware replays the original successful status/body and safe `ETag` or
`Location` headers for the same fingerprint, returns 409 when the same key has
different input, and reports an in-progress operation with retry information.
Only explicitly marked endpoints are buffered. Phase 14 defines and tests the
store contract; Phase 15 must supply its PostgreSQL implementation and join
business idempotency to the owning transaction before marking an endpoint.

## Request and response safety

- API request bodies are limited to 256 KiB by host configuration and a
  transfer-encoding-independent middleware guard.
- CORS denies cross-origin calls by default. Any allowed development origin
  must be an explicit origin; credentials are never combined with a wildcard.
- Responses include CSP, frame denial, MIME-sniffing denial, no-referrer and
  restrictive permissions headers.
- Endpoint rate-limit responses use the common problem contract and include
  `Retry-After` when supplied by the limiter.
- CSV fields escape RFC-style separators/quotes/newlines and prefix potential
  spreadsheet formulas, including formulas hidden behind whitespace.

## Current boundary

Phase 14 exposes authentication and operational endpoints plus the OpenAPI
document. Department, supplier, request, decision, approval, policy, audit,
notification and export endpoints remain Phase 15; this guide documents the
cross-cutting contract they must use rather than claiming those endpoints exist.
