# M4 full legacy parity: research findings and build-out plan

Patient Management's registration slice is done. This records what it would take to cover the *full*
legacy OpenEMR patient surface, not just registration, researched against the real PHP source
(`/home/mo/repos/openemr`) rather than guessed. This is a plan, not an implementation — the features below
get built one slice at a time, the same way registration was: `Domain` rule → `Application.Ports`
interface → `Application` use case → `Infrastructure` adapter → `Api` endpoint, each with unit,
Testcontainers integration, and architecture-layering tests.

## Decisions

- **Patient portal access is excluded entirely.** Legacy portal auth (`patient_access_onsite`: its own
  password hashing, failed-login tracking, email delivery) is a fully separate authentication system from
  staff login — unrelated to AuthFacade, and never scoped anywhere in `ROADMAP.md`. Treated as a different
  bounded context, not a Patient Management concern.
- **Patient merge targets full legacy parity, but last, blocked on other services existing.** Legacy merge
  dynamically discovers *every* table with a `pid`/`patient_id`/`encounter` column (`SHOW TABLES`/
  `INFORMATION_SCHEMA`, no fixed list) and repoints or hard-deletes rows across all of them — including
  `form_encounter`, `billing`, `forms`, which belong to Clinical Care (M5) and other not-yet-built
  services. It is also not transactional anywhere in legacy — a real risk, not something to copy as-is.
  Build this once the services owning those other tables exist, with a real cross-service strategy
  (events/saga), not before.

## The five features

1. **Duplicate detection** (`DuplicatePatientService.php`) — low risk, self-contained. A weighted score
   (email/DOB/name/sex exact matches worth 6 each, soundex-lastname 2, shared phone 1; max 27, thresholds
   12/17 for "display"/"highlight") computed and cached (`patient_data.dupscore`) automatically on every
   insert, purely advisory — never blocks registration. A separate admin report clusters high-scoring
   pairs for manual review. A real `Patient`-aggregate-adjacent domain rule (the scoring formula) with a
   thin persistence/report side.

2. **Insurance data** (`InsuranceService.php`, `insurance_data`) — real logic, not plain CRUD. Up to 3
   effective-dated "slots" per patient (primary/secondary/tertiary, `UNIQUE KEY (pid, type, date)`), an
   `EffectiveInsuranceRule` engine picking the one in-force policy per level on a given day, and a
   transactional `swapInsurance()` operation that works around the unique constraint with a sentinel date.

3. **Advance directives** (`PatientAdvanceDirectiveService.php`) — not just the two `patient_data` flags
   (`completed_ad`, `ad_reviewed`), as first assumed. A document-management subsystem (rows in the
   generic `documents` table, typed `living_will`/`durable_power_attorney`/`dnr_order`) that also
   auto-generates FHIR Observations/DocumentReferences with LOINC codes for USCDI v5 (~960 lines). The two
   flags are status markers on top of this.

4. **FHIR Patient API** (`FhirPatientService.php` + REST controllers, ~2,085 lines) —
   `POST`/`PUT`/`GET`(search)/`GET`(by id)/`$export`; no `DELETE`/`$everything`/`$match` (legacy doesn't
   have them, so no parity work needed there). 15 search parameters, several multi-column (`identifier`,
   `name`→4 columns, `address`→7 columns, `telecom`). Create/update already reuse the same
   `PatientService::insert()`/`update()` the plain registration path uses, so the new work here is almost
   entirely the ~12 bidirectional field-mapping functions (identifiers, telecom, address, and extensions
   for race/ethnicity/gender-identity/tribal-affiliation/interpreter-needed) plus the search parameters,
   not new persistence logic. `$export` (bulk data) is a separate, large subsystem — flagged, not
   committed to.

5. **Patient merge** — see decision above.

## Build-out sequence

1. Duplicate detection scoring (smallest; proves the hexagonal pattern extends cleanly to a second
   feature)
2. Insurance data (CRUD + the effective-dating rule + the swap operation)
3. Advance directives (document storage first; FHIR Observation/DocumentReference generation as a
   stretch, not required for a first cut)
4. FHIR Patient API (read + search first; write reusing the existing registration path; extension
   mapping)
5. Patient merge — once M5+ exist, revisit with a real cross-service strategy, not before

## Status: 1–4 done, 5 still deferred

Built 2026-11-13/14, each as its own branch/PR, in the order above. One correction surfaced while
building 4: this service's own registration path turned out to have no `update()` equivalent at all
(only create) — the assumption above that write would "reuse the existing registration path" held for
create, not update. `PUT` (update) was deferred rather than built, alongside legacy's own admitted incompleteness in its
write-direction field mapping (see `docs/m4-domain-notes.md`'s FHIR section for the detail). Patient
merge (5) remains deferred until M5+ services exist, as planned.
