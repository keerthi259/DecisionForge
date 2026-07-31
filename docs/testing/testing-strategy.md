# Testing strategy

## Phase 2 baseline

The solution contains the complete backend test-project structure required by
`spec.md`:

- domain and application unit tests;
- infrastructure and API integration-test projects;
- architecture and contract-test projects;
- a performance-test project; and
- framework-neutral shared test utilities.

At this phase, tests validate target frameworks, central package versioning, and
the production project-reference graph. Integration projects do not claim
PostgreSQL or HTTP coverage yet; their Testcontainers and API fixtures are added
only in the phases that introduce persistence and API behavior.

Architecture tests inspect project references and compiled dependencies. The
policy itself has a regression test containing a forbidden Domain-to-
Infrastructure edge, and phase evidence records a temporary real project-file
violation being detected.

## Commands

Run the complete backend baseline:

```powershell
./scripts/test.ps1
```

```bash
./scripts/test.sh
```

The scripts perform a locked restore before running all discovered test
projects. The build scripts additionally verify formatting, compile in Release,
and run frontend formatting, lint, type checking, and production build:

```powershell
./scripts/build.ps1
```

```bash
./scripts/build.sh
```

Coverage thresholds, PostgreSQL Testcontainers, Playwright, accessibility,
mutation, and performance budgets are introduced and enforced by their assigned
Atomic Task Graph phases. No metric is claimed before it is measured.
