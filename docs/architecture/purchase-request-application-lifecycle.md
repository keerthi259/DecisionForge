# Purchase-request application lifecycle

## Scope

Phase 9 implements request commands and requester queries. Phase 10 connects
the submission boundary to policy selection and decision orchestration. It adds
no ASP.NET Core endpoint, EF Core adapter, authentication implementation or
approval visibility. Those boundaries remain assigned to later ATG phases.

## Trust and ownership

Create, update, item, withdraw, clone, list and detail operations obtain the
requester UUID from `ICurrentUserContext`. No mutating contract contains a
requester or owner field. Repository and projection query methods require the
trusted requester UUID, and missing and non-owned resources produce the same
`purchase-request.not-found` result to avoid exposing resource existence.

The context is a port only. ASP.NET Core Identity supplies its implementation
in Phase 13; Phase 9 unit tests use deterministic trusted-context doubles.

## Commands and concurrency

`PurchaseRequestLifecycleService` provides create draft, update metadata, add
item, update item, remove item, withdraw and clone operations. It uses injected
`TimeProvider`, `IIdGenerator` and `IPurchaseRequestNumberGenerator`. Every
mutable existing-request command carries an expected concurrency token.
Successful mutations rotate the token; stale tokens return
`domain.concurrency-conflict`; no-op edits do not persist or rotate.
The number port atomically reserves the next human-readable number; its later
PostgreSQL adapter must use the database uniqueness constraint as the final
race-safe guard.

Item contracts contain description, quantity, unit price and category only.
Line and request totals are calculated by the aggregate, so no client-supplied
total can influence stored state.

Clone reads an owned source using its expected token and creates an independent
Draft with a new request number, request UUID, item UUIDs and token. It copies
only controlled domain values and recomputes the total. The source is not
modified.

## Queries

`IPurchaseRequestQueries` exposes requester-scoped list and detail projections,
not aggregates or future EF entities. List input validates a non-negative
offset, page size from 1 through 100, an optional controlled status and an
allow-listed sort enum. Returned item collections are defensive read-only
copies. Approver/auditor query scope remains Phase 11/13 work.

## Submission preconditions

`PurchaseRequestSubmissionPreconditionValidator` returns an immutable ordered
error list rather than throwing on the first business precondition. It checks:

- Draft state;
- at least one item;
- expected delivery date is not in the past using injected UTC time;
- department existence, active state and threshold-currency match; and
- supplier existence and active state.

The validator consumes reference query projections that distinguish missing
from inactive records. Phase 10 reuses the validated projections to build the
approved normalized fact snapshot before policy evaluation.

## Idempotency boundary

`IPurchaseRequestSubmissionIdempotencyStore` is a requester-scoped lookup by
`IdempotencyKey`. A completed record stores the server-calculated lowercase
SHA-256 operation fingerprint and original purchase-request result reference.
An absent key allows execution, the same fingerprint owner-scopes and replays
the original decision, and a different fingerprint returns
`purchase-request.idempotency-conflict` before request loading. The lookup port
has no standalone write: Phase 10's explicit decision transaction receives the
request, decision and record together. Phase 15 supplies the PostgreSQL
implementation of that atomic contract.
