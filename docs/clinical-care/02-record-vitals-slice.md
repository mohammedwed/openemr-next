# Clinical Care, slice 2: Record Vitals

- **Status:** Designed first (playbook MIG-3), then built. See section 8 for the verification level.
- **Milestone:** ROADMAP M5. Builds on [slice 1](01-create-encounter-slice.md) and
  [ADR 0064](../architecture/0064-clinical-care-shares-the-legacy-database-and-coexists-with-legacy-encounter-writers.md);
  the unit decision is [ADR 0065](../architecture/0065-vitals-are-stored-in-us-units-and-the-api-takes-explicit-units.md).
- **Method:** playbook slice cycle. **Every legacy citation is to the code the stack runs, OpenEMR 8.0.0**, read inside
  the container on 2026-11-16 (slice 1 learned that the newer source tree differs). Paths are relative to
  `/var/www/localhost/htdocs/openemr`. The table shape is from `snapshots/db.sql`.

## 1. Context in one paragraph

A **vitals** record is one set of measurements (blood pressure, weight, height, temperature, pulse, respiration,
oxygen saturation, circumferences, a note) taken during an encounter. It belongs to exactly one encounter and, through
it, one patient. Clinical Care owns it. Slice 2 records a set and reads sets back, and derives BMI.

## 2. Research notes (step 1)

### Where legacy writes vitals (the writers, LEG-8)

| # | Writer | Evidence |
|---|---|---|
| 1 | `VitalsService::saveVitalsArray`, reached by the vitals form, the REST API (`EncounterService::insertVital`) and FHIR | `src/Services/VitalsService.php` |
| 2 | CDA import | `src/Services/Cda/CdaTemplateImportDispose.php` (`INSERT INTO form_vitals`) |

`form_vitals` is empty on this stack. A separate table, `form_vitals_calculation`, also has a `uuid_registry`
definition; it is out of scope and not understood (section 7).

### What recording vitals does (`VitalsService::saveVitalsArray`, 8.0.0)

1. Requires an encounter and a patient (`eid` or `pid`), else throws.
2. Creates a `uuid` through `UuidRegistry` for `form_vitals`, which also inserts a `uuid_registry` row
   (`table_name = 'form_vitals'`).
3. `INSERT INTO form_vitals SET ...` with the whitelisted fields of `FormVitals`.
4. `addForm(eid, "Vitals", <new id>, "vitals", pid, authorized)`: a `forms` row with `formdir = 'vitals'` and
   `form_id` = the vitals row id. **The two inserts are not in a transaction.**
5. Optionally saves per-measurement `form_vital_details` rows (interpretation, reason code).

The form controller (`interface/forms/vitals/C_FormVitals.class.php`, `default_action_process`) adds, before saving:

- **BMI** `= weight / height / height * 703` when both are above zero, then **`BMI_status`** from `BmiCategory::fromBmi`.
- **`temp_method` is cleared** when the temperature is `0` or blank.
- `user` and `groupname` come from the session (`authUser`, `authProvider`); `authorized` is the session's 0/1
  `userauthorized` flag (confirming slice 1's finding F-CC-10); `activity` is `1`; `date` is now.
- Non-numeric values are **silently ignored** (`FormVitals::set_*` only accept `is_numeric`); there are no range checks.

### Units (see ADR 0065)

Stored: lb, in, °F, always. `units_of_measurement` (here `1`) only changes **reads**: for 2 or 4,
`createResultRecordFromDatabaseResult` converts to kg, cm, °C using `MeasurementUtils` (`0.45359237`, `2.54`,
`(F - 32) * 5/9`, six decimals). A stored `0` reads back as absent.

### BMI categories (`Common/Forms/BmiCategory`, 8.0.0)

Strictly greater than: `> 42` Obesity III; `> 34` Obesity II; `> 30` Obesity I; `> 27` Overweight;
`> 25` Normal BL; `> 18.5` Normal; `> 10` Underweight; otherwise no category.

### How a vitals row finds its encounter

`form_vitals` has **no `encounter` column** (m2 inventory, anti-pattern 4). The link is `forms`:
`forms.form_id = form_vitals.id AND forms.formdir = 'vitals'`, then `forms.encounter` is the encounter **number** and
`forms.pid` the patient. Reads are `deleted = 0 AND formdir = 'vitals'` (`getVitalsForForm`,
`getVitalsForPatientEncounter`), ordered by encounter date then vitals date, descending.

### Schema facts (`snapshots/db.sql`)

`form_vitals.id` PK, `uuid` UNIQUE, `pid` indexed. `bps` and `bpd` are `varchar(40)`; every measurement is
`decimal(12,6) DEFAULT 0.000000`; `oxygen_saturation` is `decimal(6,2)`; `temp_method`, `note` and `BMI_status` are
`varchar(255)`. `activity` and `authorized` are `tinyint(4)`.

## 3. Rule catalogue (step 2)

`CV-n` is the rule id. **(new)** means it differs from legacy and was decided here (KD-7).

| Id | Rule | Legacy evidence | Parity / new | Enforced in |
|---|---|---|---|---|
| CV-1 | Vitals are recorded against an **existing encounter** that legacy has not deleted. The patient is the encounter's. | the REST route resolves the encounter; the UI works inside one | Parity; the existence check is the use case's | `RecordVitalsUseCase` via `IEncounterReader` |
| CV-2 | At least one measurement must be supplied. | none: legacy saves an empty form | **(new)** | `VitalsRecording.Create` |
| CV-3 | Each measurement is a positive number within a plausibility bound. The bounds catch typing and unit mistakes; they are not clinical judgement. | none: non-numeric is silently dropped, no ranges | **(new)** (bounds in section 4) | `VitalsRecording.Create` |
| CV-4 | Weight, height, temperature, waist and head circumference carry an **explicit unit** and are converted to US units for storage. | storage is US units (`MeasurementUtils`) | Storage is parity; the explicit unit is **(new)** (ADR 0065) | `UnitConversion`, `Quantity` |
| CV-5 | BMI is derived from weight and height (`lb / in² × 703`) when both are present, and is not accepted as input. | `C_FormVitals::default_action_process` | Formula parity; no manual BMI is **(new)** | `BmiRule` |
| CV-6 | BMI status is the `BmiCategory` for the BMI; none at or below 10. | `BmiCategory::fromBmi` | Parity | `BmiRule` |
| CV-7 | A temperature method is kept only with a temperature; without one it is cleared. | `default_action_process` | Parity | `VitalsRecording.Create` |
| CV-8 | Blood pressure values are whole numbers, stored as text. | `bps`/`bpd` are `varchar(40)`; REST `validateVital` requires numeric | Parity (the numeric rule) | `VitalsRecording.Create` |
| CV-9 | The record is stamped with the clock's now, `activity = 1`, and the acting provider's username and group. | `FormVitals` constructor; `populate_session_user_information` | Parity in content; provider caller-supplied (F-CC-1) | `Vitals.Record`, `EfVitalsRegistry` |
| CV-10 | A `forms` row (`form_name 'Vitals'`, `formdir 'vitals'`, `form_id` = the vitals id, the encounter's number and pid, `authorized 1`) is written in the **same transaction**. | `saveVitalsArray` step 4 | Content parity; atomicity **(new)** | `EfVitalsRegistry` |
| CV-11 | The uuid is registered in `uuid_registry` with `table_name = 'form_vitals'`. | `UuidRegistry` | Parity | `EfVitalsRegistry` |
| CV-12 | An absent measurement is stored as `0` and read back as absent. | the column defaults; `if ($val != 0)` | Parity (tolerant read, DOM-9) | `EfVitalsRegistry`, `EfVitalsReader` |
| CV-13 | `VitalsRecorded` goes to the outbox in the same transaction. Its payload names the measurements taken, not their values. | none (in-process events only) | **(new)** (EVT-5: values are clinical data; a consumer asks the API) | `EfVitalsRegistry` |
| CV-14 | Reads return only non-deleted `vitals` forms of the encounter, newest first, bounded (API-6). Responses carry the stored unit; `?units=metric` converts. | `getVitalsForPatientEncounter` | Parity; the bound is **(new)** | `EfVitalsReader`, `UnitConversion` |

### Deferred or excluded, with the precondition (KD-8)

| Item | Status | Precondition |
|---|---|---|
| Update or delete vitals | Deferred | Concurrency token (LEG-10), audit (SEC-9), writer inventory |
| Per-measurement details (`form_vital_details`: interpretation, reason code) | Deferred | A code-list port for `observation_interpretation`; clinical review |
| Oxygen flow rate, inhaled oxygen concentration | Deferred | A need; rarely used |
| Paediatric percentile fields (`ped_*`) | Deferred | Patient age, which Clinical Care does not hold |
| `form_vitals_calculation` | Excluded | Not understood (section 7) |
| Growth charts, vitals history trends | Excluded | A reporting context |

## 4. Plausibility bounds (CV-3, all **(new)**)

Wide on purpose: a value outside is almost certainly a typo or a wrong unit. All compare after conversion to the
stored unit.

| Measurement | Stored unit | Bound |
|---|---|---|
| Weight | lb | `> 0` and `<= 1500` |
| Height | in | `> 0` and `<= 120` |
| Temperature | degF | `>= 50` and `<= 120` |
| Waist, head circumference | in | `> 0` and `<= 200` |
| Systolic, diastolic | mm[Hg] | whole number, `1` to `400` |
| Pulse | /min | `1` to `400` |
| Respiration | /min | `1` to `150` |
| Oxygen saturation | % | `> 0` and `<= 100` |
| Note | text | at most 255 characters (the column) |
| Temperature method | text | at most 255 characters (the column) |

## 5. Design (as planned)

- **Domain:** `Vitals` (aggregate), `VitalsRecording` (validated value object; collects every error), `Quantity`
  (value and unit), `MeasurementUnit`, `UnitConversion` and `BmiRule` (pure, cited), `EncounterReference`
  (encounter uuid, number and the patient reference), `VitalsIdentity` (uuid), `BmiCategory`.
- **Ports:** `IVitalsRegistry`, `IVitalsReader`, `VitalsRecordedEvent`. The use case reuses `IEncounterReader`.
- **Use cases:** `RecordVitalsUseCase`, `GetVitalsUseCase`, `ListEncounterVitalsUseCase`.
- **API** (under `/api/v1/encounters/...`, so the **existing gateway route already covers it**):
  `POST /api/v1/encounters/{encounterUuid}/vitals`, `GET /api/v1/encounters/{encounterUuid}/vitals`,
  `GET /api/v1/encounters/{encounterUuid}/vitals/{vitalsUuid}`. Optional `?units=metric` on the reads.
- **Data:** writes `form_vitals`, `forms`, `uuid_registry`, `clinical_care_outbox`. New grant: `SELECT, INSERT` on
  `form_vitals`. No new table, so no schema change.

## 6. Ownership matrix rows

| Table | Service does | Source of truth | Other writers | Grants | Failure behaviour |
|---|---|---|---|---|---|
| `form_vitals` | INSERT, SELECT | Shared | `VitalsService` (UI, REST, FHIR), CDA import | `SELECT, INSERT` | Whole transaction rolls back |
| `forms` | INSERT, SELECT | Shared | every legacy form writer | as slice 1 | same transaction |

## 7. Findings and not-verified

| Id | Finding | Sev | Status |
|---|---|---|---|
| F-CV-1 | `form_vitals_calculation` exists and has a uuid definition; its role (derived vitals?) is not understood. If legacy reads it when showing vitals, a vitals row written here may lack a calculated row. | M | Open: check in the UI |
| F-CV-2 | Legacy silently drops non-numeric input and has no ranges, so existing rows can hold values this service would reject. Reads are tolerant; only new writes are bounded. | L | By design |
| F-CV-3 | Vitals values are returned to any caller holding the gateway secret; there is no per-patient authorization (same as encounters, F-CC-1, F-CC-2). | H | Open, tracked |

| Claim | Why unverified | How to verify |
|---|---|---|
| Legacy's UI shows a vitals row written by this service without `form_vitals_calculation` | Reasoned | Open the vitals form for the encounter in the browser (needs an OpenEMR login) |
| Legacy's FHIR `Observation` resource handles our row | Not run | Call legacy's FHIR API for the patient's vitals |
| `bps`/`bpd` as integer text matches what legacy writes | Reasoned from `varchar` and `validateVital`; the form was not run | Save vitals in the legacy UI and compare |

## 8. Build status and verification level (QLT-15)

| Step of the slice cycle | State |
|---|---|
| 1 Research, 2 Specify | Done, from the **running 8.0.0 container** (sections 2 to 4) |
| 3 Domain | **Built and tested.** `Vitals`, `VitalsRecording`, `UnitConversion`, `BmiRule`; 155 unit tests (all slices) pass |
| 4 Ports and use cases | **Built and tested** against fakes |
| 5 Adapter | **Built and tested:** 114 integration tests (all slices) pass against MariaDB 12.3.3 with the real DDL and the real seed script |
| 6 API and contract | **Built and tested:** `POST`, list and get under `/api/v1/encounters/{uuid}/vitals`; OpenAPI snapshot and `Client` regenerated; drift guard passes |
| 7 Deploy | **No new route or secret** (the existing `/api/v1/encounters/**` gateway route covers it). One new grant (`form_vitals`: `SELECT, INSERT`), applied to the live database by re-running the idempotent seed |
| 8 Docs | This document, ADR 0065, changelog, ROADMAP |
| 9 Review | Not done |
| 10 Register | F-CV-1 to F-CV-3 above |

## 9. End-to-end verification against the running stack (2026-11-16)

Through the real gateway, on the slice-1 smoke-test encounter:

| Check | Result |
|---|---|
| `POST .../vitals` with `70 kg`, `175 cm`, `37 degC`, `120/80`, pulse 64, SpO2 98 | **201**; BMI **22.854881**, "Normal" |
| `form_vitals` row | `weight 154.323584`, `height 68.897638`, `temperature 98.600000` (US units), `bps '120'`, `bpd '80'`, `BMI_status 'Normal'`, `user admin`, `groupname Default`, `authorized 1` |
| `forms` row | `form_name Vitals`, `formdir vitals`, `form_id` = the vitals id, `encounter 3`, the patient's pid |
| `uuid_registry` | `form_vitals` |
| `GET` (US, default) | `154.323584 lb`, `68.897638 in`, `98.6 degF` |
| `GET ?units=metric` | `70 kg`, `175.000001 cm`, `37 degC` |
| **Legacy's `VitalsService::getVitalsForPatientEncounter`** | Finds the row, with `eid 3`, the right patient uuid, US units and BMI |
| **Legacy's `EncounterService::getVitals`** (the REST route's path) | Returns it |
| **Legacy's `VitalsService(2)`** (metric display) on the same row | `70.000000 kg`, `175.000001 cm`, `37.000000 Cel`: **identical to our `?units=metric`**, including the `175.000001` rounding artifact |

The last row is the strongest parity evidence: the conversion rule matches legacy's own to the last digit.

**Still open:** the legacy **UI** for this record (it needs an OpenEMR login), and F-CV-1 (`form_vitals_calculation`).
