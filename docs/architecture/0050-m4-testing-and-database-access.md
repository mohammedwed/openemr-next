# 0050. M4 uses Testcontainers, and a least-privilege database user of its own

- **Status:** Accepted
- **Date:** 2026-11-13
- **Builds on:** [0006](0006-black-box-integration-tests.md), [0043](0043-authfacade-session-state-in-a-dedicated-mariadb-database.md), [0049](0049-patient-management-writes-into-the-legacy-database.md)

## Context

Two setup questions needed an answer before M4's code gets written, both flagged as "before coding"
items rather than left to emerge mid-implementation like the aggregate's own shape.

## Decision

1. **M4's integration tests use Testcontainers**, not the black-box-against-the-already-running-stack
   pattern AuthFacade and Gateway both use ([0006](0006-black-box-integration-tests.md)). Each test run
   gets its own throwaway database rather than sharing the Compose stack's `openemr` database — a
   deliberate departure from this repo's existing single pattern, chosen because M4 writes to that shared
   legacy database directly (ADR 0049); isolating each test run from leftover state other tests or a
   running dev stack might have left behind matters more here than it did for Gateway's routing-only
   tests.
2. **Patient Management gets its own least-privilege MariaDB user**, scoped to `patient_data` and its own
   new outbox table only — not OpenEMR's existing broader application user. Matches the precedent already
   set for AuthFacade's own dedicated database user ([0043](0043-authfacade-session-state-in-a-dedicated-mariadb-database.md))
   and the roadmap's own "least-privilege access" cross-cutting workstream.

## Consequences

- This repo now has **two** integration-test patterns, not one: black-box-against-the-shared-stack
  (AuthFacade, Gateway) and Testcontainers (M4 onward). Future services should pick deliberately between
  them based on the same reasoning as here — direct writes to shared/legacy state favor Testcontainers'
  isolation; routing-only or read-mostly services fit the existing pattern fine — not default to whichever
  was used most recently.
- A new seed step (matching `scripts/seeds/authfacade-client.php`'s and the dedicated-database setup's
  own precedent) needs to provision the new user and grant it access to exactly `patient_data` and the new
  outbox table, nothing else in the `openemr` database.
- M4's own CI step will need to provision whatever Testcontainers requires in the GitHub Actions runner
  (Docker-in-Docker is already available there, since Compose itself already runs) — not yet wired up,
  since no M4 code or CI step exists yet to wire.

## Alternatives considered

- **Match the existing black-box pattern for consistency.** Rejected in favor of isolation, given M4
  writes directly to shared legacy state in a way Gateway's routing tests never touched.
- **Reuse OpenEMR's existing database credentials.** Rejected: grants far more access than M4 needs,
  against the project's own least-privilege principle and the precedent ADR 0043 already set.

## Related decisions

Builds on [0006](0006-black-box-integration-tests.md) (the pattern this departs from, for other services),
[0043](0043-authfacade-session-state-in-a-dedicated-mariadb-database.md) (the least-privilege-user
precedent), and [0049](0049-patient-management-writes-into-the-legacy-database.md) (why M4 touches the
shared database directly in the first place).
