# Clinical Care M5b: research for update, delete and audit

- **Milestone:** ROADMAP M5b (update, delete, audit, lookups). **Phase 0: research and decisions, no code.**
- **Date:** 2026-11-17. **Decisions:** [ADR 0068](../architecture/0068-clinical-care-updates-are-conditional-on-last-update-and-touch-only-owned-columns.md), [ADR 0069](../architecture/0069-clinical-care-audits-writes-to-its-own-append-only-table.md).
- **Method:** legacy behaviour read from the **running OpenEMR 8.0.0 container**, not the 8.6.0-dev source tree (they differ; see
  `01-create-encounter-slice.md` section 2). Column facts read from the live database (`information_schema`). The container's `grep` is
  BusyBox: no `--include`.

## 1. Who writes `form_encounter` (LEG-8)

Twenty-one PHP files write the table (excluding tests). Grouped by what they touch:

| Group | Writers | Columns | Our stance |
|---|---|---|---|
| **Encounter form and REST** | `forms/newpatient/save.php` (modes `new` and `update`), `EncounterService::updateEncounter`, `EncounterRestController` (`PUT /api/patient/:puuid/encounter/:euuid`) | Everything in the encounter form (see 2) | **Accommodate.** This is the writer M5b replaces; it stays live (ADR 0064) |
| **Other creators** | `encounter_events.inc.php` (calendar), `AppointmentService`, `receive_hl7_results.inc.php`, `oe-module-dorn`, `CdaTemplateImportDispose`, `group_attendance` | INSERT; `group_attendance` also `reason` | **Accommodate** (insert only; they do not race an update of ours except `reason`) |
| **Billing and collections** | `sl_eob_invoice`, `sl_eob_process`, `sl_eob_search`, `collections_report`, `edit_billnote`, `pos_checkout_*`, `BillingUtilities`, `deleter.php` (`last_level_billed`) | `last_level_closed`, `last_level_billed`, `stmt_count`, `billing_note`, `in_collection`, `invoice_refno`, ... | **Accommodate, never write.** Excluded columns (R8 Billing). They bump `last_update`, so a billing action makes the next encounter write conflict: the safe direction |
| **Provider assignment** | `FeeSheet.class.php` (`provider_id`, `supervisor_id`), `eye_mag_functions.php` (`provider_id`) | `provider_id`, `supervisor_id` | **Accommodate.** `provider_id` is shared with us: the token is what protects it |
| **Layout-based forms** | `forms/LBF/new.php` | An arbitrary column named by a layout field | **Accommodate.** Cannot be enumerated; the token is the only protection |
| **Merge and delete** | `merge_patients.php` (hard delete), `deleter.php` (hard delete) | Whole row | **Accommodate.** A write to a vanished row is a 404, not an error |

**Consequence:** nobody can promise a column is ours alone, so the update names the columns it owns and is conditional (ADR 0068). It is
not "single writer" and must not be described that way.

## 2. What a legacy update does (the behaviour to match)

`EncounterService::updateEncounter` (8.0.0), called by the encounter form and the REST PUT:

1. Validates (class code must be in `_ActEncounterCode`, as on create).
2. Resolves the facility name from `facility_id` and writes **both** (`facility`, `facility_id`).
3. **ACL checks:** an encounter with a `sensitivity` the caller may not see is refused; the encounter **date** is dropped from the update
   unless the caller has `encounters/date_a`.
4. `UPDATE form_encounter SET <every posted column that exists> WHERE encounter = ? AND pid = ?`. The builder skips `id`, `uuid`, `pid`
   and unknown keys. **It writes every field the form posts**, including ones the form shows read-only, so a legacy save overwrites a
   column another writer changed meanwhile. Legacy has no concurrency control; it is last-writer-wins and we cannot change that.
5. It does **not** touch the `forms` row. `form_encounter` has **no `user` column**, so the editor is not recorded either.
6. The form's `save.php` then replaces the encounter's issue links (`replaceIssuesForEncounter`). Issue linking is out of scope (slice 1 table).
7. Legacy audit: none for an update; the delete screens write `EventAuditLogger` entries into legacy's `log` table.

## 3. Vitals and delete

- **Vitals update is in place.** `VitalsService::saveVitalsArray` runs `UPDATE form_vitals SET <all columns> WHERE id = ?`, then upserts the
  detail rows. There is **no encounter or pid check** in the statement. The `forms` row is not touched on update.
- **No history.** The edit overwrites the row (the previous values are gone). That is parity; amendments with history are not built (section 6).
- **Delete is two different things, both needing the `admin/super` ACL:**
  - **A single form** (`delete_form.php`): `UPDATE forms SET deleted = 1 WHERE id = ?` plus a legacy `log` entry. Soft. This is what
    removes a vitals record (and in principle the encounter's own `newpatient` row, which CC-9 already treats as absent).
  - **A whole encounter** (`deleter.php`): hard-deletes `forms`, `claims`, `issue_encounter`, the form data tables, and deactivates billing
    and `ar_activity`. Out of scope, permanently: it reaches Billing's tables.
- **REST:** `PUT /api/patient/:pid/encounter/:eid/vital/:vid` and the encounter PUT exist. No REST delete for either.

## 4. The concurrency token (LEG-10)

| Table | Column | Definition (live database) |
|---|---|---|
| `form_encounter` | `last_update` | `timestamp`, default `current_timestamp()`, **`on update current_timestamp()`** |
| `form_vitals` | `last_updated` | `datetime`, default `current_timestamp()`, **`on update current_timestamp()`** |

Both are maintained by the database on any real change, by **every** writer, including legacy. That is what makes them usable.
Limits, accepted and documented:

- **Whole-second precision.** A client that reads after a first write at second T can miss a second write in the same second T. The window
  is one second wide and needs two writers.
- **A no-op update does not bump it** (`on update` fires only when a value changes). Harmless: nothing changed.
- **A new version column is useless:** legacy writers would not bump it.
- The value is **server-local time without a zone** (LEG-11). The token is an opaque string, never interpreted by the client.

## 5. The proposed editable set (encounter)

| Editable | Not editable here |
|---|---|
| `date`, `onset_date`, `reason`, `class_code`, `pc_catid`, `facility_id` with `facility`, `pos_code`, `provider_id` | `pid`, `encounter`, `uuid`, `id` (identity); every billing and collection column; `billing_facility`, `referral_source`, `referring_provider_id`, `ordering_provider_id`, `discharge_disposition`, `external_id`, `encounter_type_*`, `parent_encounter_id`, `date_end`, `shift`, `voucher_number` (no slice owns them); **`sensitivity`** (changing it needs the `sensitivities` ACL, F-CC-2) |

Vitals: the measurements (the whole set the create already takes) are the editable set; identity, `pid`, the encounter link and
`activity` are not. Details rows (`form_vital_details`) stay deferred.

## 6. Findings that change the plan

1. **Delete and sensitivity need authorization, not just identity.** Legacy requires `admin/super` for any delete and the
   `sensitivities` ACL for sensitive encounters. A verified actor (Phase 1b) is necessary but not sufficient: a **permission port** is
   needed (SEC-5). **Decision:** Phase 3 (delete) and `sensitivity` edits are built **after** the permission port; until then neither exists.
2. **Whole-encounter delete is excluded for good** (section 3).
3. **The update touches only `form_encounter`.** Legacy does not touch `forms` on update, so neither do we. Fewer grants than I planned:
   a column-level `UPDATE` on the owned columns of `form_encounter` and on `form_vitals`, and `UPDATE (deleted)` on `forms` for delete.
4. **Legacy's own `log` table is not enough for audit.** It is written for deletes only, is not append-only and is not in our
   transaction (ADR 0069). A delete through us does not also write there (Q1, section 7).
5. **Amendments with history are not built.** A vitals correction overwrites, as legacy does. The audit row records *which fields* changed
   (SEC-9: names, not values), so "who changed what, when" is answerable but "what was the old value" is not.

## 7. Questions and the answers (2026-11-17)

| # | Question | Answer |
|---|---|---|
| Q1 | Should a delete through Clinical Care also write a row into legacy's `log` table so the legacy audit screens show it? | **No.** One audit store; the gap is documented and revisited with the permission port |
| Q2 | `PUT` replaces the whole editable set, or `PATCH` changes a subset? | **PUT.** A shared row plus null-versus-absent JSON makes PATCH ambiguous |
| Q3 | Does a vitals correction recompute the BMI? | **Yes**, as record does (ADR 0065) |

## 8. Updated phase plan

| Phase | Content | Change from the plan |
|---|---|---|
| 1a | Audit port, table, grants; `ETag` and `If-Match`; the four events | as planned |
| 1b | Gateway validates the session; forwards a verified actor; service maps `users.uuid` to `users.id` | as planned (decision 1: include) |
| 2 | Encounter update (`PUT`) | as planned; editable set from section 5 |
| 3a | Vitals correction (`PUT`) | **moved before delete** |
| 3b | **Permission port**, then soft delete (vitals, encounter) | **new:** delete waits for authorization (finding 1) |
| 4 | Lookups and WebApp edit flows | unchanged |
| 5 | Verification | unchanged |
