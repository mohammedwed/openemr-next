# Changelog 2026-11-14

## Summary

The last two of the full legacy parity plan's four remaining features: advance directives, then the
FHIR Patient API. Advance directives bundles two independent legacy facts under one feature name — an
uploaded document and two status flags — both written directly into legacy's own tables, no new
schema. The FHIR Patient API covers `GET`/`POST` against legacy's real coded extensions (race,
ethnicity, administrative sex, gender identity, tribal affiliation, interpreter-needed), resolved
through `list_options` the same way legacy itself does; `PUT` (update) is deferred, since this
service's own registration path turned out to have no update capability to reuse, contrary to the
plan's original assumption. Also backfilled `m4-domain-notes.md` with grounding notes from all three
slices built this week (insurance, advance directives, FHIR), none of which had made it in there until
now. M4 is done except patient merge, which stays deferred until M5+ exist.

## Added — M4: advance directives (3/4)

- **Document attachment**, writing into legacy's real `documents` + `categories_to_documents` tables
  (ADR 0049 — no new table), grounded directly in `PatientAdvanceDirectiveService.php`,
  `advancedirectives.php`, and `Document.class.php`:
  - `documents.id` is **not** `AUTO_INCREMENT` — it's drawn from `sequences`, a single-row global
    counter (`QueryUtils::generateId()`/ADOdb's `GenID()`) shared by many legacy tables. Writing a new
    row has to draw from this exact counter via the same `UPDATE ... SET id = LAST_INSERT_ID(id + 1)`
    idiom, to avoid colliding with a still-running legacy writer.
  - Local-disk file storage only (legacy also supports CouchDB and a pluggable remote-storage hook —
    real options, but deployment-specific extras most installs don't use): same path convention as
    legacy's default, `{repository}/{pid}/{driveUuid}` (no extension), the same two-UUID scheme
    (`uuid` + a separate `drive_uuid`) and sha3-512 hash.
  - No dedicated "advance directive type" column exists anywhere in legacy — it classifies a document
    purely by matching keywords in the document's own `name` (or its category's name)
    (`determineAdvanceDirectiveType()`), so a document this service creates bakes the right keyword
    into its stored name for legacy's own code to classify it correctly too.
  - Tagged under the "Advance Directive" category (id 6, `codes = 'LOINC:42348-3'`), looked up by name
    rather than hardcoded — confirmed against the real compose stack's live database, not just the
    install script, that this is the only category actually seeded by default.
- **Status flags** — `completed_ad`/`ad_reviewed`/`advance_directive_user_authenticator` on
  `patient_data`, including the blank "skip" state legacy's own edit screen allows (`completed_ad` is a
  three-state field, not a boolean). Confirmed these are two independent legacy facts, not reconciled
  by any legacy logic: a patient can have `completed_ad = 'YES'` with zero attached documents, or vice
  versa — not something this service introduces.
- HTTP endpoints: `POST`/`GET /patients/{pid}/advance-directives/documents`,
  `GET .../documents/{uuid}/content`, `PUT`/`GET .../status`.
- The seed script now also grants the least-privilege user the specific privileges each new table
  needs (`documents`: select/insert; `categories`: select-only; `categories_to_documents`:
  select/insert/delete, since `REPLACE INTO` needs delete too; `sequences`: update-only) — verified
  against the live stack via `SHOW GRANTS`, alongside the real `documents`/`categories`/`sequences`
  table shapes, which matched what the adapter was built against exactly.
- Merged (PR #58).

## Added — M4: FHIR Patient API (4/4)

- **`GET /fhir/Patient/{uuid}`** (read) and **`GET /fhir/Patient`** (search, the same ~15 parameters
  `FhirPatientService::loadSearchParameters()` defines, combined with AND as exact-or-contains
  matches — legacy's own search modifiers are not replicated). **`POST /fhir/Patient`** (create) reuses
  `RegisterPatientUseCase`, the same path plain registration already uses.
- A hand-rolled FHIR R4 Patient resource shape (no external FHIR library, same approach legacy's own
  PHP classes take), targeting US Core 8.0.0 only — legacy branches across three profile versions;
  this service has no installed base to stay compatible with.
- Every coded extension (race, ethnicity, administrative sex, gender identity, tribal affiliation,
  interpreter-needed, language) resolved through `list_options`, matching legacy's own two resolution
  styles exactly: race/ethnicity read a plain code from `notes`; everything else splits
  `SYSTEM:CODE` out of `codes` (only `SNOMED-CT` and `DataAbsentReason` are actually seeded anywhere).
  Confirmed against the real compose stack's live database, not just the upgrade-script seed data.
- Ported legacy's own per-field distinction between "always add the extension, defaulting to
  Unknown" (race, administrative sex, interpreter-needed) and "omit it entirely when blank" (ethnicity,
  gender identity, tribal affiliation) — the two are not interchangeable in legacy, and are kept that
  way here.
- **A real, previously-latent bug found and fixed along the way**: `patient_data.date` (the table's own
  last-updated timestamp) was never actually written by `Patient.Register()` — nothing had read it back
  yet to notice, until the FHIR mapping became the first consumer (as `last_updated`). `Patient` now
  sets it at registration, the same moment as `regdate`, matching legacy's own `databaseInsert()`.
- **`PUT` (update) deliberately not built**: the plan assumed write would "reuse the existing
  registration path," but this service's own `IPatientRegistry` turned out to be create-only — no
  `Patient.Update()` exists anywhere yet. Building it would be genuinely new persistence work, not
  reuse, so it's deferred rather than silently cut. Legacy's own write-direction mapping is also
  self-admittedly incomplete (race/ethnicity/language are never mapped back on input — a comment in
  `parseFhirResource()` says so directly), so this service's create path matches that same gap on
  purpose. `$export`, `DELETE`, `$everything`, and `$match` are out of scope (the first per the plan,
  the rest because legacy doesn't implement them either).
- The seed script now also grants the least-privilege user read-only access to `users` (for
  `generalPractitioner`) and `list_options` (for every coded extension) — verified against the live
  stack via `SHOW GRANTS`, alongside the real `list_options` seed data for every list this mapping
  resolves against.

## Added — docs

- **`m4-domain-notes.md`** backfilled with grounding notes from all three slices built this week
  (insurance, advance directives, FHIR): insurance's zero-date tolerance and the swap sentinel-date
  workaround; advance directives' shared `sequences` counter, local-disk path convention, and
  name-based type classification; and the FHIR API's write-path gap, `list_options` resolution styles,
  and the `patient_data.date` fix above.
- `docs/m4-full-legacy-parity-plan.md` and `ROADMAP.md` updated: M4 is done except patient merge
  (deferred until M5+ exist, as always planned).

## Tests

- **140 tests** for PatientManagement, up from 77: 93 unit (up from 54), 42 integration (up from 18),
  5 architecture (unchanged). New coverage includes the shared `sequences` counter actually handing out
  distinct ids under the repository's own transaction (advance directives), the local-disk
  upload/download round trip against a real temp directory, a pid-ownership check on document
  download, the status-flag round trip, the FHIR read/search mapping against real `list_options` seed
  data (including the ethnicity-decline and deceased-date edge cases), and the FHIR create endpoint —
  all against a real Testcontainers MariaDB.
