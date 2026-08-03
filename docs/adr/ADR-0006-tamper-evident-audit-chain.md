# ADR-0006: Per-aggregate tamper-evident audit hash chain

- Status: Accepted
- Date: 2026-08-03
- Decision owners: DecisionForge maintainers

## Context

`DF-12-001` through `DF-12-004` require deterministic safe payloads, monotonic
per-aggregate sequences, the specified SHA-256 relationship and verification
that reports the first invalid sequence. This is tamper evidence within the
application database, not an independently immutable ledger.

## Decision

Each aggregate has a PostgreSQL head row containing its last sequence and hash.
Append creates the head if needed, locks it with `FOR UPDATE`, assigns the next
sequence, calculates the hash and updates the head in the caller's business
transaction. Different aggregates can proceed independently.

Payloads are flat, bounded, lexically sorted JSON objects encoded as UTF-8.
Field names deny credentials, tokens, full policy definitions and unbounded
reason text. Required free-text evidence is stored as presence, trimmed length
and SHA-256, while the owning workflow retains the original authorized value.

Hash input is the specification's direct ordinal concatenation of invariant
sequence, lowercase `D` GUIDs, controlled type strings, actor, UTC round-trip
timestamp, correlation ID, canonical payload and previous lowercase hash.
Timestamps are normalized to PostgreSQL microsecond precision before hashing.
The first event uses 64 lowercase zeroes as its previous hash.

Verification orders by sequence, checks a gap or previous-hash mismatch and
recalculates each event hash, returning the first expected invalid sequence.

## Alternatives considered

### One global chain

It gives one order but serializes all application writes and creates a global
contention point.

### Unchained row hashes

They detect payload edits but cannot detect deletion, insertion or reordering.

### External append-only ledger

This would provide a stronger immutability boundary but adds a distributed
system outside the approved Phase 12 architecture.

## Consequences

### Positive

- Deterministic verification detects payload, link and sequence tampering.
- Contention is isolated to concurrent changes of one aggregate.
- Golden hashes are portable across culture and host settings.

### Negative

- Database administrators can rewrite an entire chain and its head.
- Append for the same aggregate is intentionally serialized.
- Canonical formats are compatibility contracts and cannot change casually.

## Security impact

Safe-payload validation prevents unnecessary sensitive text from being copied
into long-lived audit rows. Hashing protects evidence integrity, not secrecy.
Audit read authorization remains Phase 13 and the API remains Phase 15.

## Operational impact

Audit and head rows are historical and use restrictive deletion. A failed
verification identifies an aggregate and first invalid sequence for diagnosis;
repair must never silently rewrite history.

## Validation

- Domain golden-hash and tamper tests cover payload, link and gap changes.
- PostgreSQL concurrent append tests prove sequences 1..N without duplicates.
- Transaction tests prove audit rolls back with business state.
