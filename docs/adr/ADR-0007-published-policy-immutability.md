# ADR-0007: Preserve published policy immutability and historical ranges

- Status: Accepted
- Date: 2026-08-01
- Decision owners: DecisionForge maintainers
- Specification: `FR-POL-03` through `FR-POL-07`; `DF-08-001` through `DF-08-009`

## Context

DecisionForge decisions must remain attributable to the exact policy version
and checksum that produced them. Allowing a published definition to change,
allocating duplicate version numbers, or permitting overlapping effective
ranges would make policy selection and historical reproduction ambiguous.
Policy authors must still be able to preserve and correct invalid draft text.
Phase 10 additionally requires technical retries and historical reproduction
to remain stable after a newer policy or changed reference data becomes current.

## Decision

`Policy` is the aggregate root and owns every `PolicyVersion`. All lifecycle
mutations require the aggregate concurrency token and rotate it on successful
change. Version numbers are allocated inside the aggregate as the previous
maximum plus one; Phase 15 will reinforce this with a unique policy/version
database constraint and optimistic concurrency.

Drafts retain the submitted JSON exactly. A checksum and parsed definition are
assigned only when strict parsing, semantic validation and aggregate
code/name consistency all succeed. Publishing requires a valid draft and
freezes its definition and checksum. Published and retired versions reject
definition updates and repeated lifecycle transitions.

Effective intervals use `[effectiveFrom, effectiveUntil)` semantics: the start
is inclusive and the optional end is exclusive. Adjacent ranges are allowed;
any positive overlap with a published or retired historical range is rejected.
Retirement preserves the version and closes an open range at the retirement
time. Structured comparison reports added, removed and modified rule IDs and
separately identifies priority, condition, outcome and default-outcome changes.

## Alternatives considered

### Mutable published row with revision history

This reduces the number of version rows but makes the authoritative definition
dependent on a secondary history mechanism and weakens checksum guarantees.

### Database sequence as the only version allocator

It can provide unique numbers, but hides the monotonic lifecycle rule outside
the aggregate and cannot protect in-memory/application tests. Database
constraints remain defense in depth rather than the sole rule.

### Closed date intervals

Inclusive end dates make an exact handover timestamp overlap. Half-open
intervals provide an unambiguous boundary and match timestamp-range practice.

## Consequences

### Positive

- Published content and checksums remain stable for decision reproduction.
- Optimistic concurrency serializes version allocation and lifecycle changes.
- Adjacent policy handovers have a precise, testable boundary.
- Invalid editor text is retained without being mistaken for publishable data.
- Audit mappings contain identifiers, status, dates and checksums, never full JSON.

### Negative

- Correcting a published policy requires a new version.
- An open-ended version must be retired before a replacement can be published.
- Persistence must load the aggregate's version metadata when allocating or
checking effective ranges.

Decision submission selects exactly one published version using the request's
UTC submission timestamp and half-open effective ranges. The request retains
the selected policy identity/checksum and immutable normalized fact snapshot
from its first attempt. A technical retry loads that exact version and reuses
that exact snapshot. The authoritative decision copies both alongside the full
rule trace and result checksums.

Historical reproduction never selects today's effective policy. It loads the
recorded version, verifies its checksum and compares a fresh deterministic
result against the immutable original without updating history.

## Security impact

Immutable publication prevents an authorized editor from silently changing
historical decision inputs. Strict validation and size limits continue to
bound hostile policy data. Audit mappings deliberately exclude full policy
JSON and free text. Checksums detect content changes but are not signatures and
do not establish author identity.

## Operational impact

Phase 15 must create restrictive foreign keys, a unique `(PolicyId, Version)`
constraint, optimistic concurrency for the policy token and an efficient index
covering status/effective timestamps. Recovery never edits a published row;
operators create and publish a corrective version. Retired versions remain
queryable and must not be cascade-deleted.

## Validation

Domain tests cover valid and invalid drafts, deterministic checksum refresh,
monotonic allocation, stale tokens, immutable publication, exact effective
boundaries, overlap rejection, retirement and structured diffs. Application
tests cover every use case, cancellation and safe audit mapping. Architecture
tests enforce immutable entities and specific cancellation-aware policy ports.
Phase 10 tests additionally cover zero/ambiguous effective selection, checksum
tampering, retry drift rejection, immutable decision evidence, idempotent
replay/conflict and unchanged-history reproduction.
