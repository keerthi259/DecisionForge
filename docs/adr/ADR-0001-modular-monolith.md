# ADR-0001: Use a modular monolith

- Status: Accepted
- Date: 2026-07-31
- Decision owners: DecisionForge maintainers
- Specification: `DF-01-005`; sections 4, 8, 9, 10 and 25 of `spec.md`

## Context

DecisionForge must deliver one explainable procurement workflow spanning
requests, controlled policy evaluation, decisions, approvals, audit, outbox,
identity and reporting. These capabilities share consistency boundaries:
business state, audit events and outbox messages must commit atomically, and a
decision must remain linked to its exact policy evidence. The project is built
by a small team as one reviewer-facing product and explicitly excludes
microservices, multiple databases, Kafka, Kubernetes and event sourcing.

The architecture still needs strong boundaries so deterministic domain and
policy logic can be tested without ASP.NET Core, EF Core, PostgreSQL or React.

## Decision

Build DecisionForge as one deployable modular monolith with one PostgreSQL
database and these backend dependency directions:

```text
Api -> Application
Api -> Infrastructure
Infrastructure -> Application
Infrastructure -> Domain
Application -> Domain
Domain -> no solution project
```

The React production build will be served by the ASP.NET Core host from the same
origin. Modules will expose explicit application use cases and specific
repository/query ports. Business logic will remain outside controllers,
persistence mappings and React components. Architecture tests introduced in
Phase 2 will enforce project references and layer independence.

Revisit this decision only if measured operational or organizational needs
cannot be satisfied by vertical scaling, modular boundaries and isolated
background processing. Any change requires a superseding ADR and must preserve
decision reproducibility, authorization, audit and atomicity requirements.

## Alternatives considered

### Microservices

Separate deployables could scale or release independently, but would introduce
network failure modes, distributed authorization and tracing, duplicated
contracts, and distributed transaction problems without a demonstrated need.
It is also an explicit non-goal for this implementation.

### Layered monolith without module boundaries

A conventional presentation/business/data split is initially simple but tends
to couple unrelated features through shared services and generic repositories.
It would make the policy engine and business ownership boundaries harder to
reason about and enforce.

### Serverless functions per use case

Functions could offer independent scaling but would fragment the same-origin
application and transactional workflow, complicate local reproducibility, and
add deployment surface without evidence of value.

## Consequences

### Positive

- Business state, audit and outbox can share explicit PostgreSQL transactions.
- Local execution, debugging, testing and deployment remain understandable.
- Domain and policy logic stay framework-independent and deterministic.
- A single same-origin deployment supports secure cookie authentication.
- Module boundaries can later be measured before any extraction is considered.

### Negative

- Modules deploy and scale together.
- Poorly reviewed dependencies could erode boundaries inside the process.
- Long-running outbox work must be isolated carefully from request processing.
- Database schema and migration ownership require discipline across modules.

## Security impact

A single host and same-origin browser model reduce cross-origin and distributed
credential exposure, but compromise of the host has a broad blast radius.
Deny-by-default endpoint and resource authorization remain mandatory. Module
boundaries are not security boundaries; trusted user context, antiforgery,
redaction, safe errors and database constraints must be enforced explicitly.

## Operational impact

Operations manage one application image, one PostgreSQL database, one controlled
migration step and one in-process outbox worker. Health, traces, metrics and
logs must distinguish API, database, policy, approval and worker operations.
The production container must run as non-root and serve both the API and React
assets.

## Validation

- Phase 2 architecture tests will enforce the approved dependency graph.
- The solution structure will match section 10 of `spec.md`.
- Later transaction integration tests will verify atomic business, audit and
  outbox writes against PostgreSQL.
- The Phase 23 production image test will verify one non-root deployable.
