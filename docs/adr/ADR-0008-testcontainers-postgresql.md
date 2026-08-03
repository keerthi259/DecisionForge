# ADR-0008: Testcontainers PostgreSQL integration testing

- Status: Accepted
- Date: 2026-08-03
- Decision owners: DecisionForge maintainers

## Context

`DF-12-010` and the testing baseline require transaction, locking, retry and
hash behavior against real PostgreSQL. An in-memory substitute cannot represent
PostgreSQL row locks, `SKIP LOCKED`, partial indexes, timestamp precision or
constraints.

## Decision

Infrastructure integration tests start the same pinned PostgreSQL 18.4 major
used by Aspire with Testcontainers for .NET. Each reliability test resets a
dedicated database schema and uses real Npgsql connections and transactions.
A pinned Mailpit container is part of the notification integration test.

## Alternatives considered

### Mocked data-access interfaces

Useful for application orchestration, but incapable of proving database
transaction and locking semantics.

### Developer-managed shared database

Faster after setup, but state leaks across runs and CI becomes non-reproducible.

### SQLite or EF in-memory provider

Neither implements the PostgreSQL behavior under test.

## Consequences

### Positive

- Production-specific SQL and constraints are executed as written.
- Tests are isolated and reproducible locally and in CI.
- Container image versions align with the local topology.

### Negative

- Docker is mandatory and first-run image pulls add latency.
- Integration tests cost more than unit tests.

## Security impact

Container credentials are fixed test-only values, never production secrets.
Random host ports avoid exposing a stable test database service.

## Operational impact

Docker Desktop or a compatible engine must be running. Testcontainers owns
container cleanup; failed startup is a phase-gate blocker rather than a skipped
test.

## Validation

The Phase 12 infrastructure suite runs PostgreSQL and Mailpit tests with zero
conditional skips and reports their executed counts.
