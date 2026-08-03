# Authentication API

The implemented Phase 13 authentication surface is rooted at `/api/v1/auth`.
All responses containing session or antiforgery information are intended for a
same-origin HTTPS client.

| Method and route | Authentication | Antiforgery | Result |
|---|---|---|---|
| `GET /antiforgery` | Anonymous allowed | Not required | Request token and `X-XSRF-TOKEN` header name |
| `POST /login` | Anonymous allowed | Required | 204 and secure session cookie |
| `POST /logout` | Required | Required | 204 and expired session cookie |
| `GET /me` | Required | Not required | User ID, email, display name, sorted roles and permissions |

Login accepts only `email` and `password`; identity, roles, persistence state
and lockout fields cannot be supplied. Invalid credentials always return the
same `authentication.invalid-credentials` response. Locked accounts return
`authentication.locked-out`; the endpoint limiter returns
`authentication.rate-limit`. Missing/mismatched antiforgery state returns
`authentication.antiforgery-invalid`.

Errors contain a stable `errorCode` and trace ID and omit exceptions, password
details and account-existence information. The complete cross-cutting problem
details and OpenAPI conventions remain Phase 14 scope.
