# Policy contract and deterministic evaluation

## Supported schema version

DecisionForge accepts policy documents with `schemaVersion` exactly `1.0`.
Unknown versions fail with `policy.schema.unsupported`; they are never treated
as the current version. Published policy JSON must retain its version and
canonical checksum so a later decision can use the identical contract.

## Closed JSON shape

The root requires `schemaVersion`, `policyCode`, `name`, `defaultOutcome` and
`rules`. Rules require `id`, a non-negative integer `priority`, `when` and
`then`. Outcomes require a controlled `disposition`, `reasonCode` and
`message`. `requiredApproverRoles` is permitted only for
`ManualApprovalRequired`, where at least one unique role is required.

Conditions are a closed recursive AST:

- comparison: `fact`, `operator` and scalar `value`;
- membership: `fact`, `in` or `notIn`, and a non-empty scalar array;
- existence: `fact` and `exists` or `notExists`, with no value;
- logical: exactly one non-empty `all` or `any` array; or
- negation: exactly one `not` child.

Objects reject unknown and duplicate properties. Controlled values and
operators are case-sensitive. The fact registry is the sole mapping of the
sixteen approved paths to their types, values and operators. Policy input is
data: it is never compiled, reflected over, scripted or executed as code.

## Operators

`equals` and `notEquals` compare same-typed strings ordinally, decimals
exactly and booleans directly. Numeric ordering uses exact decimal comparison.
`contains` performs a case-sensitive ordinal substring comparison. `in` is
true when any configured value equals the fact; `notIn` negates that complete
membership result. Duplicate membership values do not change the result.
`exists` and `notExists` observe fact presence without requiring a value.

A comparison or membership against an absent fact fails safely with
`policy.evaluation.missing-fact`. Unknown paths and type mismatches are
controlled failures, never dynamic lookups or conversions.

## Deterministic evaluation algorithm

1. Validate the immutable policy contract and typed fact set.
2. Canonicalize facts by path and compute the lowercase SHA-256 input checksum.
3. Order rules by ascending priority and then ordinal rule ID.
4. Evaluate every rule and every logical child, recording condition results and
   fact accesses. `all` and `any` deliberately do not short-circuit so the
   explanation trace is complete.
5. Aggregate matched outcomes using `Rejected` over
   `ManualApprovalRequired` over `AutoApproved`. Apply the default outcome only
   when no matched rule requires rejection or manual approval.
6. Union roles from contributing matched rules and the applied default, remove
   duplicates, and order them `Department`, `Procurement`, `Security`,
   `Finance`, `Senior`. De-duplicate reasons by code while preserving the
   deterministic contributing-rule order, followed by the default reason when
   applicable.
7. Canonicalize the complete result and rule/condition trace and compute its
   lowercase SHA-256 trace checksum.

The result, its nested reasons, rule trace, condition trace and fact-access
trace are immutable. Reordering input facts or source rules cannot change the
result or checksums. Rule order is observable only in the canonical
priority/ID order.

## Limits and cancellation

| Resource | Maximum |
|---|---:|
| UTF-8 policy JSON | 256 KiB |
| Rules | 100 |
| Condition depth | 10 |
| Children in `all` or `any` | 25 |
| Values in `in` or `notIn` | 100 |
| Condition evaluations per run | 2,500 |
| Rule ID or reason code | 64 characters |
| Reason message | 500 characters |

Cancellation is checked before evaluation and at every rule and condition
node. Cancellation propagates as `OperationCanceledException`. Depth or total
execution-limit breaches fail with `policy.evaluation.execution-limit`; no
partial result is returned. Parser and validation failures remain normalized
`path`, `code`, `severity`, `message` records. Evaluation failures expose a
stable code, safe path and non-sensitive message.

## Canonical policy representation

Canonical policy serialization writes fixed property order, normalized codes
and text, exact controlled names, invariant decimals without insignificant
trailing zeroes, and no formatting whitespace. Optional role arrays are
omitted when empty. The policy checksum is lowercase hexadecimal SHA-256 over
the UTF-8 canonical bytes. The checksum identifies content; it is not a
signature and does not establish publisher trust.
