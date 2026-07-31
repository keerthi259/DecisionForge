# Contributing to DecisionForge

DecisionForge is implemented phase by phase from the Atomic Task Graph in
[spec.md](spec.md). A change is acceptable only when it stays within the active
phase, includes its required tests and documentation, and provides real command
evidence.

## Before making a change

1. Read `spec.md` completely and identify the relevant task IDs and phase gate.
2. Review [docs/project/phase-status.md](docs/project/phase-status.md) and the
   accepted architecture decisions under `docs/adr`.
3. Run `scripts/validate-tools.ps1` on Windows or
   `scripts/validate-tools.sh` on Bash-capable systems.
4. Create a focused branch and do not mix unrelated work into the change.

## Engineering rules

- Preserve the modular-monolith dependency direction defined by the spec.
- Do not add placeholders, fake success paths, disabled tests, silent exception
  handling, secrets, or fabricated evidence.
- Do not introduce future-phase projects or abstractions early.
- Keep domain and application code independent of transport and persistence.
- Treat authorization, antiforgery, concurrency, idempotency, audit integrity,
  and redaction as correctness requirements.
- Pin dependencies and commit lock files when dependency management is added.
- Add an ADR for a material architectural decision or a deviation from the
  approved baseline.

## Validation and evidence

Run the active phase gate and affected checks before requesting review. Record
the exact command, date, environment, result, and source artefact according to
[docs/evidence/evidence-policy.md](docs/evidence/evidence-policy.md). A claim is
not evidence unless the command was executed and its output was inspected.

Commit messages should be imperative and scoped when useful, for example
`docs(governance): establish Phase 1 baseline`. Pull requests must list the
implemented task IDs, tests run, known issues, security impact, and any spec
deviation.
