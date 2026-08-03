# ADR-0005: Transactional outbox in the application process

- Status: Accepted
- Date: 2026-08-03
- Decision owners: DecisionForge maintainers

## Context

`DF-12-003`, `DF-12-005` through `DF-12-010` require business state, audit
events and asynchronous messages to commit atomically, followed by bounded and
idempotent delivery. A direct database commit followed by direct email has a
crash window that can lose work. Adding a broker would expand the topology and
contradict the modular-monolith baseline.

## Decision

DecisionForge writes outbox rows with business state and audit rows in one
caller-owned PostgreSQL transaction. An in-process hosted worker claims bounded
batches with `FOR UPDATE SKIP LOCKED`, a time-bounded lease and a database
attempt count. Failures return to pending with capped exponential delay; the
configured last attempt becomes visibly failed. Completed rows alone are
eligible for bounded age-based cleanup.

Handlers receive a stable message ID as their delivery/idempotency identity.
In-app notifications have a unique source-outbox constraint. The Mailpit
development adapter sends that identity as both `Message-ID` and a dedicated
header. Delivery is at least once; downstream notification senders must honor
the stable delivery identity.

Phase 12 supplies the reliability persistence component and Testcontainers
schema. Phase 15 will express these tables in the reviewed application
migration together with the remaining business schema; no startup schema
creation or silent migration is introduced here.

## Alternatives considered

### Direct post-commit delivery

Simple, but a process failure after commit and before delivery loses the
notification. Reversing the order can notify about rolled-back state.

### Kafka, RabbitMQ or a cloud broker

These can be appropriate at larger scale, but add distributed operations and
do not remove the need to bridge the database commit atomically.

### Third-party background-job framework

This adds storage conventions and abstractions not required by the bounded
outbox behavior.

## Consequences

### Positive

- Database commit is the single durable handoff.
- Leasing supports concurrent workers without duplicate claims.
- Retry, terminal failure and cleanup rules are explicit and testable.
- No new runtime service is required.

### Negative

- Delivery is at least once and every external sender needs idempotency.
- PostgreSQL stores the transient queue and requires backlog maintenance.
- Phase 15 must keep the reviewed schema consistent with the tested contract.

## Security impact

Outbox payloads are bounded canonical JSON and exclude approval free text,
policy JSON, credentials and tokens. SQL uses parameters. Terminal failures
store a controlled code rather than exception messages. Notification email
content is intentionally delivered to Mailpit in local development and must
not contain secrets.

## Operational impact

The worker may be disabled until the Phase 15 migration has created its tables.
Operators retain pending and failed messages; only completed messages older
than retention are deleted. Backlog metrics and the operational runbook remain
Phase 21 work.

## Validation

- PostgreSQL integration tests prove atomic commit/rollback and lease races.
- Dispatcher tests prove backoff, bounded terminal failure and cancellation.
- Cleanup tests prove pending and failed rows remain.
- A real Mailpit container proves the local adapter produces a visible message.
