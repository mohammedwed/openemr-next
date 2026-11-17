# Clinical Care M5d slice 1: correct recorded vitals

- **Milestone:** ROADMAP M5d (finish Clinical Care), slice 1. **Decisions:** [ADR 0068](../architecture/0068-clinical-care-updates-are-conditional-on-last-update-and-touch-only-owned-columns.md) (the concurrency model), [ADR 0065](../architecture/0065-vitals-are-stored-in-us-units-and-the-api-takes-explicit-units.md) (units), [ADR 0069](../architecture/0069-clinical-care-audits-writes-to-its-own-append-only-table.md) (audit). **Builds on** [02](02-record-vitals-slice.md) (recording) and [06](06-update-encounter-slice.md) (the update pattern this reproduces).
- **Endpoint:** `PUT /api/v1/encounters/{encounterUuid}/vitals/{vitalsUuid}` with `If-Match`, and an optional `?units=us|metric` for the response. **Status:** built; suites green (section 5).

## 1. What it does

Replaces a recorded set of vitals with the **whole measurement set** the caller sends, if and only if the vitals are still the version the caller read, and returns them as stored with a new `ETag`.
Status codes, headers and the order of checks are the encounter update's: 200, 400, 401, 404, 412 (with the current `ETag`), 428. Legacy edits vitals **in place with no history**, and so does this; the
audit row records which measurements changed, never what they were.

## 2. Rules (continuing CV-n in `02-record-vitals-slice.md`)

| Id | Rule | Legacy source | Parity | Where |
|---|---|---|---|---|
| CV-15 | The correction replaces the whole measurement set: a measurement **left out is not recorded** (stored as `0`, which reads back as absent, CV-12), so a correction can remove a reading, and must still leave at least one (CV-2). | `VitalsService::saveVitalsArray` writes every column | Parity | `VitalsChange`, `EfVitalsUpdater` |
| CV-16 | **A correction accepts exactly what a recording accepts and refuses exactly what it refuses**: one implementation of the unit rules, bounds, whole-number readings, oxygen rounding, text limits and BMI serves both. A test runs the same inputs through both and compares the messages. | the REST and form paths share `VitalsService` | One owner per rule (DOM-2) | `VitalsFieldRules` |
| CV-17 | The BMI and its category are **recomputed** from the corrected weight and height and cleared when either is removed; they are never sent, and not listed as a changed measurement. | `FormVitals` calculation | Parity | `VitalsFieldRules`, `EfVitalsUpdater` |
| CV-18 | The update applies only if the row's `last_updated` still equals the version the caller read; otherwise nothing is written. | none (last writer wins) | **(new)**, LEG-10 | `EfVitalsUpdater` |
| CV-19 | A correction reaches **only vitals that belong to the encounter in the URL** (matched through the `forms` registry row on encounter number and patient) and that legacy has not soft-deleted. Another encounter's vitals, or another patient's, are a 404, not an update, even by their exact uuid. | legacy's `UPDATE ... WHERE id = ?` checks neither | **(new)**, stricter | `EfVitalsUpdater` |
| CV-20 | Only the measurement columns are written (`weight`, `height`, `temperature`, `temp_method`, `bps`, `bpd`, `pulse`, `respiration`, `oxygen_saturation`, `waist_circ`, `head_circ`, `BMI`, `BMI_status`, `note`). Who recorded the vitals and when (`user`, `groupname`, `date`), `authorized`, `activity`, the uuid and the `forms` row are not touched, as legacy's update leaves them. The database user holds `UPDATE` on exactly those columns. | the update statement | Parity | `EfVitalsUpdater`, seed script |
| CV-21 | If nothing differs from what is stored, nothing is written, no event is emitted and no audit row is written. The comparison is at storage precision (every stored measurement is already rounded to six places). | none | **(new)** | `VitalsChangeSet` |
| CV-22 | One transaction holds the conditional `UPDATE`, the `VitalsCorrected` outbox row and the audit row (`vitals.correct`); if the audit insert fails the correction does not commit. The event and the audit row name the **measurements** that changed, never values. | none | **(new)**, EVT-1, EVT-5, SEC-9 | `EfVitalsUpdater`, use case |
| CV-23 | The audit actor is **unknown** (`verified = 0`): a correction names no acting user, and putting a provider id there would put a wrong name in the trail. | the session's user | Deliberate; needs identity forwarding (ADR 0069 #5) | use case |
| CV-24 | A unit the API does not know is a 400 decided before the record is read, in the same shape as every other validation failure, listed together with whatever else is wrong. | as recording | Parity with recording | endpoint |

## 3. Known limitations

| # | Limitation | Why accepted |
|---|---|---|
| L1 | Whole-second stamp; a lost commit acknowledgement is reported as 412 on retry, never a second write. | As for encounters (06, L1 and L2) |
| L2 | The editor is not identified (CV-23). | No verified identity until Phase 1b |
| L3 | **No history.** A correction overwrites; the old value cannot be recovered from this service. | Parity with legacy (research doc, finding 5); amendment history is deferred by decision |
| L4 | Per-measurement details (`form_vital_details`: interpretation, reason code), oxygen flow and the paediatric percentiles are not carried and are **left as they are** by a correction. | As recording (02, section 3) |
| L5 | Free-text blood pressure that legacy holds (`"120/80"`) reads as absent (DOM-9), so correcting it to a number counts as a change of that measurement. | Tolerant read |
| L6 | Delete is still not offered. | The permission port (legacy needs `admin/super`) |

## 4. Contract

`VitalsResponse` now carries an `eTag`, equal to the `ETag` header on a single read and present on every list item, so a listed set can be corrected without a read per row (as for the other updates).
`If-Match` is a declared header parameter on the operation, and 412 and 428 are documented, so the generated client can send it.

## 5. Tests

| Layer | What it pins |
|---|---|
| Unit (domain) | `VitalsChange`: stored US units and BMI, **the same inputs through recording and correction give the same values or the same messages**, no provider rule, a reading can be removed, at least one measurement, all violations together |
| Unit (application) | `VitalsChangeSet` (one measurement at a time, null versus empty text, a removed reading, free-text blood pressure); the use case against fakes: order of checks, no-op writes nothing, conflict at write time, the vitals found through their own encounter, event and audit content, refusals audited |
| Integration (adapter) | Every measurement column changes and the BMI is recomputed; a left-out measurement is `0`; who recorded it and the `forms` row are untouched; a legacy edit between read and write is detected; **vitals of another encounter or patient, and soft-deleted vitals, are not found**; identical values still match; a failing audit rolls the correction back; the column-level grant (allowed on exactly the 14 columns, denied on identity, who and when) |
| Integration (HTTP) | 200, 400, 401, 404, 412, 428 and the `ETag` round trip; either unit system; removing a reading; the order of checks; no-op writes nothing; audit and event rows; a list item's `eTag` is the one a correction needs |
