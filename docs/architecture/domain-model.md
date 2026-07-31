# Domain model

## Phase 5 boundary

`DecisionForge.Domain` contains procurement state and rules only. It references
no other solution project and has no ASP.NET Core, EF Core, Npgsql or dependency
injection dependency. Phase 5 adds reference-data aggregates and the policy-fact
boundary. Policy parsing/evaluation, persistence and API contracts remain later
phases.

```mermaid
classDiagram
  AggregateRoot <|-- PurchaseRequest
  AggregateRoot <|-- Department
  AggregateRoot <|-- Supplier
  Entity <|-- AggregateRoot
  Entity <|-- PurchaseRequestItem
  PurchaseRequest "1" *-- "0..*" PurchaseRequestItem
  PurchaseRequest --> PurchaseRequestMetadata
  PurchaseRequest --> RequestNumber
  PurchaseRequest --> CurrencyCode
  PurchaseRequest --> Money
  PurchaseRequestItem --> Money
  PurchaseRequestItem --> ProcurementCategory
  EvaluationFactSnapshot --> PurchaseRequest
  EvaluationFactSnapshot --> Department
  EvaluationFactSnapshot --> Supplier
  AggregateRoot --> IDomainEvent
```

## Reference data

`Department` has an immutable normalized code, bounded name, non-negative money
threshold, active flag and optimistic concurrency token. `Supplier` has an
immutable normalized registration number, bounded name, controlled approval,
onboarding and risk states, active flag and concurrency token. Onboarding is
modeled independently from approval using `NotStarted`, `InProgress`,
`Completed` and `Suspended`, so a suspended onboarding condition is not
conflated with the supplier's commercial approval state.

Creation is active by default. Detail and activation mutations require the
current token, a distinct caller-supplied next token and a monotonic UTC time.
Stale tokens return `domain.concurrency-conflict`; repeating the current active
state returns `domain.invalid-state`. Codes and registration numbers cannot be
changed. Application services use injected `TimeProvider` and `IIdGenerator`
and persist only successful state changes.

## Evaluation facts

`EvaluationFactSnapshot.Create` is the sole public construction path. It joins
a request to matching active reference aggregates, rejects currency mismatch,
past delivery dates, missing items and item-count overflow, then copies exactly
the approved request, department, supplier and derived fact paths. Fact records
have no public constructors or setters, preventing policy consumers from
forging or mutating inputs.

Technology is derived from software, hardware or cloud-service line items.
Urgency exception is derived from urgent or emergency requests. A request with
one line category exposes that category; a mixed-category request exposes the
controlled `Other` category while technology remains independently derived.

## Request aggregate

A new request receives its UUID, human-readable request number, requester UUID,
currency and UTC creation time from its caller. This makes construction
deterministic and avoids hidden clock or identifier generation. The requester
is immutable ownership data. A new request is a `Draft`, has no items, has a
zero total in its declared currency and raises one creation event.

Only the aggregate adds, changes or removes owned items. Item quantities and
unit prices must be positive, descriptions are bounded, categories are
controlled enums and line totals must fit PostgreSQL `numeric(18,2)`. Item
currency must equal request currency. The request never accepts a client total;
it recalculates the total from line totals after every successful mutation.
Overflow and validation failures are checked before state changes, preventing a
failed operation from partially mutating the aggregate.

Metadata consists of non-empty department and supplier UUIDs, urgency, data
sensitivity, expected delivery date and an optional bounded business
justification. Metadata and items are immutable once the request leaves Draft.

## Implemented transitions

```mermaid
stateDiagram-v2
  [*] --> Draft
  Draft --> Submitted: Submit with at least one item
  Submitted --> Evaluating: Begin evaluation
  Evaluating --> EvaluationFailed: Technical failure
  EvaluationFailed --> Submitted: Authorized retry
  Submitted --> Withdrawn: Withdraw
```

Every operation checks its allowed source state. Repeated submission,
withdrawal, evaluation start or retry is rejected with
`domain.invalid-state`. Withdrawal from `PendingApproval` is recognized by the
aggregate contract, while creation of approval state remains Phase 10/11 scope.
All mutation timestamps must be UTC and cannot precede the aggregate's previous
change.

## Domain events

Creation, metadata changes, item addition/change/removal, submission,
evaluation start/failure/retry and withdrawal raise distinct immutable events.
Events contain identifiers and controlled values needed by later application
and audit mapping; business justification and item description text are not
copied into events.

## Value objects and errors

The required value objects are immutable and construction-validated:
`Money`, `CurrencyCode`, `RequestNumber`, `PolicyVersionNumber`,
`PolicyChecksum`, `ReasonCode`, `DepartmentCode`,
`SupplierRegistrationNumber`, `BusinessJustification`, `CorrelationId`,
`IdempotencyKey`, `ConcurrencyToken` and `AuditHash`.

`Money` is non-negative, has at most two decimal places, is bounded to
`numeric(18,2)`, and rejects mixed-currency or overflowing arithmetic. Domain
failures expose stable codes for validation, invalid state, missing/duplicate
entities, currency mismatch, amount overflow, optimistic-concurrency conflict,
inactive reference and reference mismatch.
