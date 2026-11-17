# Clinical Care M5d slice 2: research for immunizations

- **Date:** 2026-11-17. **Decisions:** [ADR 0072](../architecture/0072-immunizations-are-versioned-by-content-because-the-table-has-no-usable-stamp.md). Reuses ADR 0069 (audit) and the encounter and issue patterns for the rest.
- **Method:** legacy behaviour read from the running OpenEMR 8.0.0 container and its database (not the 8.6 source tree). The table is empty in this stack.

## 1. What legacy has

One table, `immunizations` (31 columns), unique on `id` and `uuid`, indexed on `patient_id`. Its own table, not shared with other record types, so the type-scoping rule of ADR 0070 does not arise.

| Group | Columns |
|---|---|
| Identity | `id`, `uuid` (nullable), `patient_id` (the patient's pid), `encounter_id` (an encounter **number**) |
| Which vaccine | `immunization_id` (int, the `option_id` of list `immunizations`), `cvx_code` (varchar 64) |
| The administration | `administered_date` (datetime), `administered_by` (text), `administered_by_id` (users.id), `ordering_provider` (int), `route`, `administration_site`, `amount_administered` (**float**), `amount_administered_unit` |
| The product | `manufacturer`, `lot_number`, `expiration_date` |
| Statements | `education_date`, `vis_date` (the Vaccine Information Statement) |
| Outcome | `completion_status`, `information_source`, `refusal_reason`, `reason_code`, `reason_description`, `note` |
| Bookkeeping | `create_date`, `created_by`, `updated_by`, `update_date`, `added_erroneously`, `external_id` |

## 2. Who writes it (LEG-8)

| Writer | How | Notes |
|---|---|---|
| The issue screen (`immunizations.php`) | **`REPLACE INTO immunizations SET id = ?, ...`** for add *and* edit | `REPLACE` deletes the row and inserts a new one: **every column the form does not set is reset to its default**. It passes the row's `uuid` back from a hidden field, so an edit keeps it; an add passes none |
| The same screen, "added in error" | `UPDATE immunizations SET added_erroneously = ? WHERE id = ?` | The non-destructive way legacy marks a mistaken entry |
| The same screen, delete | `DELETE FROM immunizations WHERE id = ? LIMIT 1`, with a legacy audit-log entry | Hard delete |
| CCDA and CCR import, list maintenance | Bulk inserts and a rename of a list option | Any column |
| **REST and FHIR** | **Read only.** `GET /api/immunization`, `GET /fhir/Immunization`. The service's `insert` and `update` answer "Method not implemented yet", and its validator is empty | This service would be the first API that can write them |

**Consequences.** Legacy has **no written validation** for an immunization (the service is a stub, the validator empty, and the screen has no required field, so an empty record is accepted). Every rule below is therefore new, and labelled so.
A legacy edit overwrites the whole row, so "last writer wins" is not merely theoretical here.

## 3. The finding that shapes the design: there is no usable version stamp

`update_date` is `timestamp NOT NULL` with **no default and no `ON UPDATE`** (this MariaDB runs with `explicit_defaults_for_timestamp`). Nothing maintains it, and the screen's `REPLACE` never sets it. Every other
updatable table in this milestone has a stamp the database moves for every writer, which is what ADR 0068 relies on. **This one does not**, so the stamp-based `ETag` cannot be used.
Adding a version column to a legacy table is ruled out (LEG-1: nothing is migrated), and would in any case be reset to NULL by the screen's `REPLACE`.

**Decision (ADR 0072): the version is a hash of the row's owned columns, and the write verifies it under a row lock.** A change by *any* writer to *any* owned column changes the hash, which is exactly what the token has to detect. It does not notice
a change to a column this service does not own, which is correct: that cannot affect what a correction writes. The write runs in a transaction that first locks the row (`SELECT ... FOR UPDATE`), recomputes the hash from the locked values and compares it
with the one the caller read, so a legacy edit landing between the read and the write is still caught.

## 4. Other facts that shape the rules

- **`amount_administered` is a 4-byte `float`.** A decimal does not survive it exactly, so amounts are rounded to four places on the way in and on the way out, and the diff and the hash use the rounded value.
- **`note` and `reason_description` are `text`: 65,535 bytes, not characters.** With multi-byte characters a long note passes a character count and fails the column, so the limit is checked in **bytes** (the lesson of the first review, R-1 to R-7).
- **`encounter_id` is an encounter number**, not a uuid. The API takes the encounter's uuid and resolves it, and refuses an encounter of another patient (the rule IE-3 gave links).
- **A row created in the screen has no uuid until something backfills it** (the add passes none), so it is not addressable here: the same accepted gap as ADR 0070 #6.
- **`added_erroneously`** is legacy's "entered in error": a boolean the screen toggles. It is part of the editable set, which gives this slice a non-destructive way to void a mistaken entry without a delete.
- **Seven code lists** back the coded fields (`immunizations`, `drug_route`, `immunization_administered_site`, `immunization_informationsource`, `immunization_refusal_reason`, `drug_units`, and `Immunization_Completion_Status`, whose id has a capital `I`). Option ids are compared exactly.

## 5. Proposed rules, all new except where stated

| Id | Rule |
|---|---|
| IM-1 | The administered date is required, between 1900-01-01 and a day from now (an administration is in the past; a day of slack covers zones). |
| IM-2 | The vaccine is identified: at least one of the `immunizations` list code and a CVX code. |
| IM-3 | Each coded field, if given, is a configured code, compared exactly, and fits its column. |
| IM-4 | Text fields fit their columns; the two `text` fields are limited in **bytes**. |
| IM-5 | An amount, if given, is greater than 0 and at most 100,000, rounded to four places, with a unit when an amount is given. |
| IM-6 | The expiration, education and VIS dates, if given, are real dates in the 1900 to 100 years range; the expiration is not before the administered date. |
| IM-7 | An encounter, if given, must exist and be the same patient's; its number is what is stored. |
| IM-8 | `enteredInError` is part of the set and can be set and cleared. |

## 6. Proposed slices of the work

Record, get and list a patient's immunizations; correct (a `PUT` with `If-Match`); entered-in-error as a field of the correction. No delete (it needs the permission port, and `enteredInError` covers the clinical need).
