# Changelog 2026-11-14

## Summary

The third of the full legacy parity plan's four remaining features: advance directives. Two
independent legacy facts, bundled under one feature name — an uploaded document and two status flags
— both written directly into legacy's own tables, no new schema. Also backfilled `m4-domain-notes.md`
with grounding notes from this and the previous (insurance) slice, neither of which had made it in
there yet.

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
- Pushed to `feat/patient-advance-directives`, **not yet merged**.

## Added — docs

- **`m4-domain-notes.md`** backfilled with grounding notes from both this slice and the previous
  (insurance) one, which had shipped without being recorded there: insurance's zero-date tolerance and
  the swap sentinel-date workaround, and advance directives' shared `sequences` counter, local-disk
  path convention, and name-based type classification.

## Tests

- **119 tests** for PatientManagement, up from 77: 83 unit (up from 54), 31 integration (up from 18),
  5 architecture (unchanged). New coverage includes the shared `sequences` counter actually handing out
  distinct ids under the repository's own transaction, the local-disk upload/download round trip
  against a real temp directory, a pid-ownership check on document download, and the status-flag round
  trip — all against a real Testcontainers MariaDB.
