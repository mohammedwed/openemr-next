# Clinical Care M5d slice 2: record, read, list and correct immunizations

- **Milestone:** ROADMAP M5d, slice 2. **Research:** [14](14-immunizations-research.md). **Decision:** [ADR 0072](../architecture/0072-immunizations-are-versioned-by-content-because-the-table-has-no-usable-stamp.md) (versioned by content). **Builds on** ADR 0068 (the update contract), 0069 (audit), 0064 (shared database).
- **Endpoints:** `POST /api/v1/immunizations`, `GET /api/v1/immunizations/{uuid}`, `GET /api/v1/immunizations?patientUuid=` (paged), `PUT /api/v1/immunizations/{uuid}` with `If-Match`. **Status:** built; suites green (section 5).

## 1. What it does

Records an immunization on a registered patient, reads and lists them, and corrects one by replacing its whole editable set, if and only if it is still the version the caller read. It is the **first API of any kind that can write
this table**: legacy's REST and FHIR surface is read-only and its service's write methods are stubs, so every rule below is new, and labelled so.
The contract is the other updates' (200 with the new `ETag`, 400, 401, 404, 412 carrying the current `ETag`, 428, same order of checks), with one difference: the `ETag` is a **content version** (`"c-<32 hex>"`), because the table has no stamp (research 14, section 3).

## 2. Rules

| Id | Rule | Legacy source | Parity | Where |
|---|---|---|---|---|
| IM-1 | The administered date is required, between 1900-01-01 and a day from now (an administration is in the past; a day covers zones), stored to the whole second. | none (the screen accepts anything) | **(new)** | `ImmunizationFacts` |
| IM-2 | The vaccine is identified: at least one of an `immunizations` list code and a CVX code. | none | **(new)** | `ImmunizationFacts` |
| IM-3 | Each coded field (vaccine, route, site, completion status, information source, refusal reason, amount unit), if given, is a configured code compared **exactly**, and fits its column. Blank means none. | the screen's drop-downs | Parity in effect | `ImmunizationFacts`, `EfImmunizationCodeLists` |
| IM-4 | Text fits its column. `note` and `reason_description` are `text` (65,535 **bytes**), so they are limited by UTF-8 size: a multi-byte note that passes a character count would fail the column. | none | **(new)**, R-1 to R-7 | `ImmunizationFacts` |
| IM-5 | An amount is greater than 0 and at most 100,000, rounded to four places (the column is a 4-byte `float`), and needs a unit. | none | **(new)** | `ImmunizationFacts` |
| IM-6 | Education and VIS dates are between 1900 and a day from now; the expiration is between 1900 and fifty years ahead. **An expiration before the administered date is deliberately accepted:** giving an expired vaccine is a real, reportable event, and refusing the record would hide it. | none | **(new)** | `ImmunizationFacts` |
| IM-7 | A linked encounter must exist and be the same patient's; its **number** is what the column stores. The API takes the encounter's uuid. | the screen stores `encounter_id` unchecked | **(new)**, stricter (as IE-3) | use case, `ImmunizationFacts` |
| IM-8 | `enteredInError` is part of the editable set and can be set and cleared: legacy's non-destructive way to void a mistaken entry. There is no delete. | `mode=added_error` | Parity | `ImmunizationFacts` |
| IM-9 | **The version is a hash of the owned columns, verified under a row lock** (`SELECT ... FOR UPDATE`, re-read with the reader's own mapping, compare, then write). A change by any writer to any owned column, including the legacy screen's `REPLACE INTO` that moves no stamp, is detected, even one landing between the caller's read and this write. | none | **(new)**, LEG-10, ADR 0072 | `EfImmunizationUpdater`, `ImmunizationView.Version` |
| IM-10 | A change to a column this service does not own (for example `external_id`) is **not** a conflict: it cannot affect what a correction writes. | none | **(new)** | `ImmunizationView.Version` |
| IM-11 | `update_date` has no default and nothing maintains it, so this service sets it on record and on correction; it is **not** the token. | none | **(new)** | registry, updater |
| IM-12 | A correction reaches only the immunization of **the patient the row names**; another patient's, or an unknown uuid, is a 404. A row legacy wrote with **no uuid** is not addressable (ADR 0070 #6). | `WHERE id = ?` checks nothing | **(new)**, stricter | updater, reader |
| IM-13 | Only the owned columns and `update_date` are written and granted: not the patient, the uuid, `create_date`, `created_by`, `updated_by` or `external_id`. No `DELETE` is granted. | none | **(new)** | updater, seed script |
| IM-14 | One transaction holds the row, its `uuid_registry` row (so legacy recognises the uuid), the outbox row and the audit row; a failing audit insert rolls everything back; a retry after a lost acknowledgement writes nothing twice. The events and audit rows name **fields**, never values. | none | **(new)**, EVT-1, EVT-5, SEC-9, OPS-9 | registry, updater |
| IM-15 | If nothing differs from what is stored, nothing is written, no event is emitted and no audit row is written. | none | **(new)** | use case |
| IM-16 | The audit actor is the named provider on a record (unverified) and **unknown** on a correction (no verified identity until M5b phase 1b). | the session's user | Deliberate (ADR 0069 #5) | use cases |

## 3. Known limitations

| # | Limitation | Why accepted |
|---|---|---|
| L1 | Legacy's `REPLACE INTO` still wins silently if it runs after this service, and it also resets columns this service does not own. | Inherent to coexistence; stated in ADR 0072 |
| L2 | A correction needs a lock per call. | The table is small per patient and corrections are rare |
| L3 | No delete. | The permission port (legacy's delete needs `admin/super`); `enteredInError` covers the clinical need |
| L4 | Rows the issue screen creates have no uuid until something backfills one, so they are not listed or addressable. | As for problems and allergies (ADR 0070 #6) |
| L5 | The acting provider on a record is caller-supplied and unverified. | Finding F-CC-1 |
| L6 | The FHIR resource's `lastUpdated` reads `update_date`, which this service keeps current, but legacy's own screen never does. | Not ours to fix |
| L7 | `immunization_id` is stored as NULL when absent (legacy's screen sends an empty string, which strict mode rejects); the reader reads `0` and empty text as absent. | Tolerant read (DOM-9) |

## 4. Contract

`ImmunizationResponse` carries an `eTag` (also on every list item). `If-Match` is declared on the PUT, with 412 and 428 documented, so the generated client can send it. OpenAPI snapshot regenerated.
A stamp's tag (`"..."` hex ticks) and a content tag (`"c-..."`) are different shapes and never satisfy one another.

## 5. Tests

| Layer | What it pins |
|---|---|
| Unit (domain) | The content version (determinism, framing against collisions, absent versus empty, order, tag shape and parsing, strong tags only); every rule IM-1 to IM-8 with the exact messages; **every property of the view is either in the version or deliberately outside it**; the aggregate and its identity |
| Unit (application) | Record, correct, get, list against fakes: order of checks, no-op writes nothing, version carried to the updater, conflict at write time, encounter of another patient, refusals audited |
| Integration (adapter) | Every column round-trips; the uuid registry row, `update_date`, creator; event and audit in the same commit and a failing audit rolls back; a retry writes nothing twice; tolerant reads of empty and zero legacy values; the float amount; paging; **a legacy edit that moves no stamp is detected**; an unowned column is not a conflict; another patient's row is not found; **six concurrent corrections on one version: exactly one wins** |
| Integration (HTTP) | 201, 200, 400, 401, 404, 412, 428; the `ETag` round trip; a stamp-shaped tag is refused; clearing a field and entered-in-error; the order of checks; no-op; audit and event content |
| Integration (grants) | Update allowed on exactly the 24 owned columns; denied on patient, uuid, creation and creator columns, `external_id`; no delete |
