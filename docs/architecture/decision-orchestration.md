# Decision orchestration and reproduction

## Scope

Phase 10 connects the Phase 8 immutable policy lifecycle and Phase 9 request
application boundary to the deterministic Phase 7 evaluator. It defines domain
and application contracts only. PostgreSQL mappings, API endpoints, audit,
outbox messages and approval workflows remain assigned to Phases 11, 12 and 15.

## Effective policy selection

The policy query port returns bounded evaluation sources containing the policy
and version IDs, version number, lifecycle status, immutable parsed definition,
canonical checksum and UTC effective range. The domain selector independently
applies the half-open range `[effectiveFrom, effectiveUntil)` at the request's
submission timestamp. It accepts exactly one `Published` source. Zero or more
than one applicable source fails with a stable error; no arbitrary tie-breaker
or fallback policy exists.

An evaluation source recalculates the definition checksum when constructed.
Draft sources and checksum mismatches are rejected before evaluation. Exact
version lookup intentionally permits a retired source for retry and historical
reproduction.

## Normalized input

Submission precondition validation returns the exact department and supplier
projections it validated. `NormalizedEvaluationInputBuilder` maps only their
approved controlled fields into domain evaluation sources and then delegates
derivation to `EvaluationFactSnapshot`. Names and supplier registration data
are not copied. The resulting fact set contains the sixteen paths listed in
`policy-contract.md`; totals, categories, counts and derived flags remain
server-calculated.

The first evaluation start stores an immutable `PurchaseRequestEvaluationContext`
on the request. It contains the exact policy identity/checksum and normalized
snapshot. A failed evaluation retains this context. An authorized retry loads
the exact original version and reuses the same snapshot, so reference-data or
policy changes after submission cannot alter the retry input.

## Atomic submit/evaluate boundary

```mermaid
sequenceDiagram
  participant S as DecisionSubmissionService
  participant I as Idempotency store
  participant R as Request repository
  participant P as Policy query
  participant E as Evaluator
  participant T as IDecisionTransaction
  S->>I: Find(requester, key)
  alt exact replay
    S-->>S: Return owner-scoped original decision
  else new operation
    S->>R: Load owner-scoped draft
    S->>P: Select exactly one at submission timestamp
    S->>S: Normalize facts; Submitted -> Evaluating
    S->>E: Evaluate immutable definition and facts
    S->>S: Create Decision; map request outcome
    S->>T: Commit(request, decision, idempotency record)
  end
```

`IDecisionTransaction.CommitDecisionAsync` is the only decision write boundary.
It receives the mutated request, authoritative decision and optional submission
idempotency record together. The idempotency lookup port has no standalone add
operation. Phase 15 must implement this contract as one database transaction
and enforce a unique authoritative decision per request.

A technical evaluator exception transitions the in-memory request from
`Evaluating` to `EvaluationFailed` and commits only that controlled state through
the failure transaction method. Cancellation and domain/concurrency failures
are not translated into evaluator failures. No failure path manufactures an
approval result.

## Decision evidence and explanation

`Decision` is an immutable aggregate and owns immutable `RuleEvaluation`
entities. It records:

- purchase request, policy and exact version identity;
- policy version number and canonical checksum;
- the normalized evaluation snapshot and its checksum;
- final disposition, ordered roles and de-duplicated reasons;
- default-outcome indicator and full ordered condition traces; and
- trace checksum and UTC decision time.

The explanation query loads by purchase-request ID and trusted requester ID,
then maps this evidence without accepting an owner from input. A missing or
foreign resource returns the same not-found result.

## Historical reproduction

Reproduction owner-scopes the original decision, loads its exact recorded
version (published or retired), verifies all identity/checksum fields, and runs
the evaluator over the decision's stored normalized snapshot. The comparison
reports original and reproduced disposition, input checksum and trace checksum,
plus a full equivalence flag. It never updates or replaces the original
decision and never selects the policy currently effective today.

## Idempotency

The server calculates a canonical SHA-256 fingerprint from the operation name,
request ID and expected concurrency token. The idempotency key is scoped to the
trusted requester. A matching key/fingerprint returns the original owner-scoped
decision without loading or mutating the request. Reusing the key with a
different fingerprint returns `purchase-request.idempotency-conflict` before
request loading.
