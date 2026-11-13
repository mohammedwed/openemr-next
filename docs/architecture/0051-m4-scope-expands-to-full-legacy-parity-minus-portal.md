# 0051. M4 scope expands to full legacy patient parity, minus the portal

- **Status:** Accepted
- **Date:** 2026-11-13
- **Builds on:** [0049](0049-patient-management-writes-into-the-legacy-database.md), `docs/m4-domain-notes.md`, `docs/m4-full-legacy-parity-plan.md`

## Context

M4's original scope (ROADMAP.md) was registration only, now done (ADR 0049's design, built and tested).
The user decided Patient Management should cover the *full* legacy OpenEMR patient surface before moving
to M5, not just registration. `docs/m4-full-legacy-parity-plan.md` researched what that surface actually
is, against the real PHP source — five subsystems: duplicate detection, insurance, advance directives, the
FHIR Patient API, and patient merge. Two of them raised a real architectural question before any of this
could be planned further.

## Decision

1. **Patient portal access is excluded from Patient Management entirely.** Legacy portal authentication
   (`patient_access_onsite`: its own password hashing, failed-login tracking, email delivery) is a fully
   separate system from staff login, unrelated to AuthFacade, and was never scoped anywhere in
   `ROADMAP.md`. It is a different bounded context — a patient-facing product surface with its own
   authentication model — not a Patient Management concern. Not planned, not built, here.
2. **Patient merge targets full legacy parity eventually, but is explicitly last, and blocked.** Legacy
   merge discovers every table with a `pid`/`patient_id`/`encounter` column dynamically and repoints or
   deletes rows across all of them, including tables Clinical Care (M5) and other not-yet-built services
   will own, not Patient Management. It is also not transactional anywhere in legacy. This service will
   not reach into other services' future tables before those services exist, and will not copy legacy's
   non-transactional approach when it does. Revisit with a real cross-service strategy once M5+ exist.
3. **Four features are in scope and sequenced**: duplicate detection, insurance data, advance directives,
   and the FHIR Patient API — each a normal hexagonal slice (Domain/Ports/Application/Infrastructure/Api),
   built one at a time, the way registration was. See `docs/m4-full-legacy-parity-plan.md` for the
   researched detail on each and the build order.

## Consequences

- `ROADMAP.md`'s M4 section needs updating to reflect this expanded scope — it currently only names
  registration.
- M5 (Clinical Care) does not start until this expanded M4 is done, per the user's explicit sequencing.
- Patient merge remains a named, tracked piece of work, not silently dropped — it is deferred with a
  stated precondition (M5+ existing), not excluded the way the portal is.
- The FHIR Patient API's `$export` operation is explicitly flagged as out of scope for the planned
  build-out (a separate, large bulk-data subsystem), to avoid it being assumed included under "full
  parity."

## Alternatives considered

- **Include the portal, for true 1:1 parity.** Rejected: it is a different authentication system and a
  different bounded context; including it would mean building an entire second auth stack inside a
  service whose whole identity is "patient data," not "patient-facing authentication."
- **Build patient merge now, reaching into other services' future tables directly.** Rejected: nothing
  for it to coordinate with exists yet (M5, billing, etc.), and legacy's own non-transactional approach is
  a known risk this project should not reproduce without a deliberate decision to do so.

## Related decisions

Builds on [0049](0049-patient-management-writes-into-the-legacy-database.md) (why Patient Management
writes into the legacy database at all) and `docs/m4-domain-notes.md`/`docs/m4-full-legacy-parity-plan.md`
(the researched detail this decision is based on).
