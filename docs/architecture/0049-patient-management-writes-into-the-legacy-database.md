# 0049. Patient Management writes into OpenEMR's own database, not a new one

- **Status:** Accepted
- **Date:** 2026-11-13
- **Builds on:** `docs/m2-schema-inventory.md`, `docs/m4-domain-notes.md`

## Context

`docs/m4-domain-notes.md` left one question explicitly unresolved: does M4's `Patient.Register()` write
into a new database the Patient Management service owns, with OpenEMR's own `patient_data` read only for
cross-reference — or does registration write directly into `patient_data` itself? The user has now
decided: **M4 uses the legacy database.** This is that decision, plus the consequences it forces on
ROADMAP.md's M4 bullets, which were written assuming the other answer.

## Decision

1. **Patient registration writes directly into OpenEMR's `patient_data` table**, in the shared `openemr`
   MariaDB database — not a new database created and migrated by the Patient Management service.
2. **ROADMAP.md's M4 bullet "Own database and EF Core migrations, with a slim model scaffolded from
   `patient_data` only" no longer applies as written.** There is no new database. The slim EF model maps
   onto `patient_data` as it already exists (the same kind of mapping `OpenEmr.Data`'s scaffolded entities
   already do) — the new service does not own or migrate that table's schema.
3. **"Read-only anti-corruption adapter for `patient_data`" is no longer accurate.** The adapter now
   performs the actual insert for a new registration. It still does the anti-corruption job — shielding
   the `Patient` aggregate from the legacy schema's quirks (`docs/m4-domain-notes.md`'s identifier
   glossary, naming traps, and validation rules) — it just isn't read-only anymore.
4. **The transactional outbox table must live in the same `openemr` database, not a separate one.** The
   outbox pattern's entire guarantee — the event row and the business-data write commit together or not
   at all — only holds if both are written in one local transaction against one database. Splitting the
   patient insert and the outbox insert across two different databases turns "published exactly once"
   into a distributed-transaction problem nobody is proposing to solve here. So the outbox becomes a new
   table inside `openemr` (e.g. `patient_management_outbox`), not a table in some other store.

## Consequences

- M4's exit criterion ("the event is published exactly once") is achievable with a plain local
  transaction, specifically *because* of this decision — the moment the patient insert and the outbox
  insert happen in the same database, atomicity is free. Had M4 kept its own separate database, this
  criterion would have needed an outbox-per-database plus a relay, or a distributed transaction — real
  extra complexity this decision avoids.
- **Two open risks this ADR does not resolve, both needing a decision before the insert path is coded:**
  - *Concurrent writes from two code paths.* If OpenEMR's own legacy PHP registration UI stays live
    alongside the new service, both now write to the same `patient_data` table. `docs/m2-schema-inventory.md`
    already found this table has **no `PRIMARY KEY`**, **no `FOREIGN KEY` or `CONSTRAINT` anywhere in the
    schema** — nothing at the database level catches two code paths disagreeing about an invariant. Whether
    the legacy UI's registration path is turned off, kept as a fallback, or left running alongside the new
    one is a real product decision, not an engineering detail.
  - *Safe identity allocation under concurrency.* `docs/m4-domain-notes.md` already flagged that OpenEMR's
    own `PatientService::getFreshPid()` (`SELECT MAX(pid)+1`) is a read-then-write race. The new service
    must not reuse that pattern for its own inserts — but if the legacy PHP app can still insert
    concurrently (per the risk above), a safe allocation scheme on the new service's side alone does not
    fully close the race unless the legacy path is also accounted for. This needs its own resolution when
    the actual insert logic is written, not here.
- The identity-glossary conclusion from `docs/m4-domain-notes.md` is unaffected and still holds: `uuid` is
  the aggregate's public identity (matching OpenEMR's own FHIR API), `id` carries no real uniqueness
  guarantee and should not be relied on.

## Alternatives considered

- **Patient Management owns a new database; `patient_data` is read-only, cross-referenced only.** This was
  the original (unconfirmed) reading of ROADMAP's M4 bullets. Superseded by this ADR per the user's
  explicit decision.

## Related decisions

Builds on the findings in `docs/m2-schema-inventory.md` (no declared referential integrity anywhere;
`patient_data` has no `PRIMARY KEY`) and `docs/m4-domain-notes.md` (the identifier glossary, the
`getFreshPid()` race condition, registration validation rules). Changes ROADMAP.md's M4 section
accordingly.
