# Development Workflow

This document defines the minimum engineering practices for OpenEMR Next. It
applies to source code, infrastructure, documentation, and configuration.
When a rule conflicts with a clinical-safety, security, privacy, or legal
requirement, the stricter requirement applies.

## Coding standards

- C# code targets the .NET version recorded by the solution and is formatted
  with `dotnet format` using the checked-in `.editorconfig`.
- Nullable reference types and implicit usings are enabled unless a documented
  exception is approved.
- Warnings treated as errors in production code must not be suppressed without
  a code comment, issue reference, and reviewer approval.
- Public APIs, domain invariants, security-sensitive behavior, and migration
  assumptions require XML documentation or a nearby design document when the
  intent is not obvious from the code.
- Domain code must not depend directly on HTTP, database schemas, or legacy
  table names. Those concerns belong behind application or infrastructure
  boundaries.
- Tests use the same naming and formatting standards as production code. Tests
  should verify observable behavior and should not depend on shared mutable
  state or real clinical data.
- Secrets, tokens, passwords, patient data, and other sensitive values must
  never be committed, logged, or included in test fixtures.
- Every change should leave the code, documentation, and tests in a coherent
  state. Unrelated formatting or refactoring should be a separate change.

The repository `.editorconfig` is the source of truth for whitespace, newline,
encoding, and language-specific formatting defaults. Tool configuration may
make rules stricter, but should not silently weaken them.

## Branching and commits

- `main` is protected and must always be buildable from a clean checkout.
- Work branches are short-lived and use one of these prefixes:
  `feature/`, `fix/`, `docs/`, `refactor/`, `test/`, `build/`, or `security/`.
- Branch names should be lowercase, concise, and include the issue number when
  one exists, for example `feature/142-authentication-audit`.
- Changes enter `main` through a pull request. Direct pushes and force-pushes
  to `main` are prohibited.
- Commits should be small, cohesive, and written as an imperative summary.
  Rewriting a private work branch is acceptable; shared branches must not be
  rebased or force-pushed without agreement from affected contributors.
- Use squash merging by default so `main` has one meaningful commit per pull
  request. Preserve a merge commit only when the history has operational or
  release value.

## Pull requests and review

Every pull request must:

- Explain the problem, the approach, and any behavior or data-ownership change.
- Identify affected bounded contexts, APIs, events, migrations, security
  controls, and operational procedures.
- Include tests or explain why tests are not applicable.
- State how the change was validated and provide migration, rollback, or
  feature-flag details when relevant.
- Use sanitized or synthetic data only.
- Be small enough for reviewers to understand the complete change.

At least one maintainer approval is required for ordinary changes. Two
approvals are required when a change affects authentication or authorization,
clinical safety, patient or financial data, encryption or secrets, a public
contract, a database migration, or a production deployment procedure. The
author may not approve their own pull request, and required reviewers must
resolve requested changes before merge.

Reviewers should check correctness first, then security and privacy, data
ownership and rollback, compatibility, observability, tests, and maintainability.
Architecture changes also require an ADR or an update to an existing ADR.

## Continuous integration

CI runs for every pull request and every push to `main`. Required checks are:

1. Restore dependencies from the lock files and fail on restore errors.
2. Check formatting with `dotnet format --verify-no-changes`.
3. Build with warnings treated as errors for production projects.
4. Run unit, integration, contract, and security tests that are available for
   the changed scope.
5. Collect coverage and publish results; coverage thresholds are defined when
   the first executable slice is added.
6. Scan dependencies and containers for known vulnerabilities and fail on the
   severity threshold defined by the security policy.
7. Run secret detection and repository policy checks.

CI must use a pinned SDK version, a clean checkout, least-privilege tokens, and
no production credentials. External services used by tests must be disposable,
isolated, and populated only with synthetic data. The workflow should cache
dependencies only through supported actions and must not cache secrets.

Until the .NET solution and CI workflow exist, this document is the approved
contract for their implementation. The first solution pull request must add
the workflow and make each applicable check required in branch protection; it
must not lower the requirements merely because a check is inconvenient.

## Exceptions

An exception requires a pull request comment or ADR that records the rule,
reason, risk, owner, compensating control, and expiry or review date. Security,
privacy, and clinical-safety exceptions require approval from the relevant
maintainer in addition to the ordinary review requirement.