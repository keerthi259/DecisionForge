# Security policy

## Supported versions

DecisionForge is in pre-release development. Security fixes are applied only to
the latest commit on the default branch until the first tagged release. This
table will be replaced with explicit supported release lines when releases
exist.

| Version | Supported |
|---|---|
| Default branch | Yes |
| Unreleased snapshots and forks | No |

## Reporting a vulnerability

Do not open a public issue for a suspected vulnerability. Use the repository
host's private vulnerability-reporting feature when available. If private
reporting is not enabled, contact the repository owner privately and include:

- the affected revision and component;
- prerequisites and minimal reproduction steps;
- the observed and expected security boundary;
- potential impact; and
- any suggested mitigation.

Do not include credentials, authentication cookies, antiforgery tokens,
personal data, complete policy documents, or production data in a report.
Use synthetic values and redact logs before attaching them.

The maintainer will acknowledge a report within five business days, assess its
severity, coordinate a correction and regression test, and disclose it after a
fix is available. No response-time or certification claim is made for this
demonstration project.

## Security baseline

The authoritative security requirements are in [spec.md](spec.md), especially
the prohibited shortcuts, authorization model, STRIDE controls, mandatory
security tests, supply-chain controls, and redaction rules. Security controls
must not be weakened to make a build or test pass.
