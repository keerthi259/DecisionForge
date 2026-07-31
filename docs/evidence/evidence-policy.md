# Engineering evidence policy

## Purpose

DecisionForge uses evidence to prove specification acceptance criteria and KPI
claims. Evidence is a reproducible record of an observation, not a prediction,
placeholder, copied sample, or manually invented result.

## Required metadata

Every evidence record must identify:

- UTC execution date and time;
- specification phase and task IDs;
- exact command or documented manual procedure;
- relevant pinned tool versions and environment details;
- exit code and factual result;
- counts or measurements taken directly from source output;
- source artefact path, when output is stored separately;
- limitations, skipped work, failures and reasons; and
- the commit SHA, or `uncommitted` when evidence predates a commit.

Secrets, cookies, authorization headers, antiforgery tokens, passwords, complete
policy JSON and unnecessary sensitive free text must be redacted before evidence
is committed. Redaction must not change the meaning of a result.

## Evidence rules

1. Never mark a command `PASS` unless it executed successfully and its output was
   reviewed for warnings, skips and hidden failures.
2. Never report coverage, mutation, security, accessibility or performance
   values unless a tool measured them in the stated environment.
3. Record failed commands and blockers; do not delete inconvenient evidence or
   weaken a gate.
4. Keep concise reviewed Markdown summaries in `docs/evidence`. Store verbose or
   generated output under `docs/evidence/generated` locally or as CI artefacts;
   it is ignored unless a later phase explicitly selects a stable artefact for
   source control.
5. A rerun supersedes an earlier result only when both remain traceable through
   Git or CI history.
6. Generated migrations and API clients may use their documented exclusions;
   all other quality-gate exclusions require a narrow written justification.
7. Documentation must distinguish current evidence from future targets.

## Phase completion

Each phase report maps every atomic task to concrete files and executed evidence.
A phase is complete only when every acceptance item and the phase gate pass. A
missing tool, failed command, unmeasured mandatory KPI, or unresolved mandatory
criterion results in `PARTIAL` or `BLOCKED`, never an optimistic `PASS`.
