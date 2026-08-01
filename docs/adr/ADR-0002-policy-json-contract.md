# ADR-0002: Use a closed versioned policy AST and deterministic interpreter

- Status: Accepted
- Date: 2026-08-01
- Decision owners: DecisionForge maintainers
- Specification: `DF-06-001` through `DF-07-012`; sections 13, 31 and 32 of `spec.md`

## Context

Policy documents are administrator-controlled data that later drive business
decisions. They must be reviewable, reproducible and safe when malformed or
hostile. An open serializer model, dynamic expressions or executable scripts
would permit ambiguous shapes, uncontrolled fact access and unstable
interpretation across releases.

## Decision

Accept only schema version `1.0` through a strict hand-written JSON reader into
a closed immutable condition hierarchy. Reject unknown or duplicate
properties, unknown controlled names, invalid node combinations, type
mismatches and configured resource limits. Validate fact/operator compatibility
against one immutable registry of the sixteen approved facts.

Return controlled validation records rather than parser exceptions. Serialize
accepted contracts in a fixed canonical form and identify that form with a
SHA-256 checksum. Do not evaluate policy data during parsing or validation.

Interpret the validated AST with a pure, synchronous domain evaluator. Supply
facts through a closed typed fact set, sort rules by priority then ordinal ID,
evaluate the complete tree for traceability, and aggregate dispositions using
the mandated precedence. Produce immutable result and trace objects plus
canonical SHA-256 input and trace checksums. Check cancellation at rule and
condition boundaries and enforce a total condition-evaluation limit.

## Alternatives considered

### Attribute-based general-purpose deserialization

It requires less reader code, but unknown-member, duplicate-property,
polymorphic-shape and path reporting behavior is less explicit and easier to
weaken accidentally.

### JSON Schema as the only validator

JSON Schema could describe much of the structure, but fact/operator/type and
reason-code consistency are semantic rules. Adding it now would duplicate the
runtime contract without replacing semantic validation.

### Dynamic expressions or scripts

They provide flexible authoring but expand the execution and injection surface,
make allowed fact access harder to prove, and violate the requirement that
policy data must never be executed as code.

## Positive consequences

The accepted surface is small, deterministic and architecture-testable, and
malformed input cannot leak parser diagnostics. New facts, operators, node
shapes or incompatible meanings require deliberate code, tests and schema
version review. Complete traces make every decision reproducible and
explainable, while typed lookup prevents coercion and unapproved fact access.

## Negative consequences

The explicit reader, interpreter and canonical serializers require more code
than dynamic evaluation. Evaluating every logical child costs more than
short-circuit evaluation, and schema changes create a deliberate migration
obligation. The bounded 100-rule workload is therefore protected by measured
performance and mutation gates.

## Security impact

Closed shapes, bounded input and scalar values reduce parser resource abuse and
prevent arbitrary member or executable expression injection. Validation errors
contain stable codes and safe messages only. A checksum detects canonical
content changes but is not a signature and does not establish publisher trust.
The interpreter has no I/O, reflection or dynamic-code path; missing or invalid
facts produce controlled errors rather than accessing arbitrary members.

## Validation

Fixtures and unit/property tests cover every node, operator and approved fact,
malformed and unknown structures, semantic mismatches, exact limits, canonical
round trips and a golden checksum. Architecture tests enforce sealed immutable
contracts and prohibit executable-policy dependencies. Phase 7 also enforces
golden result/trace checksums, deterministic input-order properties, 95%/90%
focused coverage, policy-evaluator mutation thresholds and a 100-rule p95
latency budget.
