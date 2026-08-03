# ADR-0003: Same-origin secure cookie authentication for React

- Status: Accepted
- Date: 2026-08-03
- Decision owners: DecisionForge maintainers

## Context

`DF-13-001` through `DF-13-010` require local ASP.NET Core Identity accounts,
resource authorization, browser antiforgery protection, account lockout and a
bounded login rate. The production React application and API are intended to
share an origin. Browser-managed credentials must not be exposed to JavaScript
or stored in local storage.

## Decision

DecisionForge uses ASP.NET Core Identity with GUID user and role keys persisted
through EF Core and Npgsql in the application PostgreSQL database. The
application session is an eight-hour, non-sliding `__Host-` cookie that is
HTTP-only, Secure, SameSite Strict, essential and scoped to `/`. Authentication
failures return 401/403 instead of redirecting to an HTML login page.

Unsafe authentication endpoints require a cookie-bound antiforgery token sent
in `X-XSRF-TOKEN`. The request token is obtained from a no-store endpoint; it
is not an authentication credential. Login is protected by both Identity
lockout and an IP-partitioned fixed-window limiter.

Identity, role and claim persistence stays in Infrastructure. HTTP principal
adaptation, endpoint contracts, antiforgery and ASP.NET authorization handlers
stay in API. Domain and Application remain independent of ASP.NET Core and EF
Core. Resource handlers receive server-loaded authorization projections and do
not trust ownership, roles or permissions from request bodies.

Phase 13 defines the Identity model but does not create production tables at
startup. Test databases use `EnsureCreated` only inside disposable
Testcontainers. Phase 15 must include this model in its reviewed migration.

## Alternatives considered

### Browser bearer token in local storage

This simplifies some cross-origin clients but exposes a long-lived bearer
credential to JavaScript and increases the impact of script injection.

### Cross-origin cookie authentication

This requires broader CORS and cookie configuration without product value. The
approved deployment already serves the SPA and API from one origin.

### External identity provider

An external provider can be appropriate for production enterprise use, but is
outside the local demo requirement and would add operational dependencies.

### Role-only checks in endpoint code

Inline role checks are easy to scatter and cannot safely express ownership,
assigned-stage and current-state requirements. Named policies and resource
handlers keep denials centralized and testable.

## Consequences

### Positive

- Browser JavaScript cannot read the authentication cookie.
- Antiforgery validation binds unsafe requests to their cookie session.
- Identity lockout, rate limiting and secure cookie behavior use supported
  ASP.NET Core components.
- Resource access remains deny-by-default and separately testable.

### Negative

- Non-browser clients must deliberately obtain and send an antiforgery token
  for unsafe cookie-authenticated calls.
- HTTPS is required for the authentication and antiforgery cookies.
- Phase 15 must keep its migration synchronized with the Phase 13 Identity
  model.

## Security impact

The design reduces credential disclosure and CSRF risk. Residual risks include
same-origin script execution, credential stuffing within configured limits and
misconfigured reverse-proxy HTTPS. Passwords and antiforgery values are never
logged, returned by `me`, or committed in application configuration. Demo
identities require both an explicit setting and a Development/Demo environment.

## Operational impact

Operators must supply the connection string and, when demo seeding is enabled,
the demo password through secret-aware configuration. Role/demo seeding is
idempotent and opt-in. Production schema creation remains a controlled
migration operation.

## Validation

- Real PostgreSQL tests verify Identity persistence and idempotent seeding.
- API tests verify secure cookie flags, login/logout/session restoration,
  antiforgery rejection, lockout and rate limiting.
- Authorization matrices verify owner, assigned approver, auditor, role
  separation, completed-stage and explicit override-permission behavior.
- Architecture tests preserve Domain/Application framework independence.
