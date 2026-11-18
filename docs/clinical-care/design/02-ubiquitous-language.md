# Clinical Care: ubiquitous language

- **Part of:** the Clinical Care design set (`README.md` in this folder)
- **Rule:** one word, one meaning, in the code, the API, the screens and these documents. Where legacy uses a word differently, the table in section 3 says so, and the code uses ours.

## 1. Glossary

### The visit and what is recorded at it

| Term | Meaning | In code | Legacy / wire |
|---|---|---|---|
| **Encounter** | One visit of one patient. Has a public `uuid` and a per-installation **number** | `Encounter`, `EncounterIdentity` | `form_encounter` row; `encounter` column is the number |
| **Encounter number** | The number legacy screens show and join on. Allocated from one shared counter under a lock; **not** the row id | `Encounter.Number` | `form_encounter.encounter`, from `sequences` |
| **Class** (visit class) | The kind of visit: outpatient, emergency, and so on. A code from a configured list; the default is the one flagged default, else the first | `EncounterClassRule`, `ClassCode` | `class_code`, list `_ActEncounterCode` |
| **Calendar category** | The visit category a calendar shows. Required | `CalendarCategoryId` | `pc_catid` |
| **Facility** | Where it happened. Its *name* and default place-of-service are copied onto the encounter when it is created | `FacilityId`, `FacilityName`, `PosCode` | `facility_id`, `facility`, `pos_code` |
| **Provider** | The user who acted: the verified user, not a body field | `ProviderId` | `provider_id` |
| **Vitals set** | One recording of measurements at an encounter | `Vitals`, `VitalsRecording` | a `form_vitals` row plus a `forms` registry row |
| **Measurement** | One value in a vitals set (weight, height, temperature, pulse and so on) | `Quantity`, `MeasurementKind` | a `form_vitals` column |
| **Unit system** | How a read is shown: US or metric. **Storage is always US** | `UnitSystem`, `MeasurementUnit` | stored in lb, in, °F |
| **BMI** | Derived from weight and height by legacy's own formula; stored with a category label | `BmiRule` | `BMI`, `BMI_status` |
| **Problem** | A condition on the patient's problem list | `Problem` | `lists` row, `type = 'medical_problem'` |
| **Allergy** | An allergy or intolerance on the allergy list | `Allergy` | `lists` row, `type = 'allergy'` |
| **Issue** | Either of the two above, when it does not matter which | `IssueReference`, `IssueFieldRules` | a `lists` row |
| **Issue link** | A problem or allergy *addressed at* an encounter, with whether it was resolved at that visit | `EncounterIssueLink` | `issue_encounter` row |
| **Immunization** | A vaccine given to the patient | `Immunization`, `ImmunizationFacts` | `immunizations` row |
| **CVX code** | The national vaccine code. A vaccine is named by the configured list *or* by its CVX code | `CvxCode` | `cvx_code`, `immunization_id` |
| **Entered in error** | An immunization recorded by mistake: marked, never removed | `EnteredInError` | `added_erroneously` |
| **SOAP note** | A note with four sections (subjective, objective, assessment, plan). Addressed by its **form id within its encounter**, because it has no uuid | `SoapNote`, `SoapSections` | `form_soap` row plus a `forms` row |
| **Clinical note** | A typed, dated, free-text note with a type and an optional category. Has a uuid | `ClinicalNote`, `ClinicalNoteFacts` | `form_clinical_notes` row, with its own parent form |

### How a write is made safe

| Term | Meaning | In code |
|---|---|---|
| **Concurrency token** | The row's own last-update stamp, offered as a strong `ETag`. Whole-second precision | `ConcurrencyToken` |
| **Content version** | A hash of the columns this service owns, offered as an `ETag` that starts `"c-`. Used where a row has no usable stamp | `ContentVersion` |
| **Precondition** | The `If-Match` header a conditional write must carry. Missing or `*` is refused (428); stale is refused (412) | `Preconditions` |
| **Idempotency key** | An `Idempotency-Key` header on a creating request: the same key and request returns the first answer | `Idempotency`, `IIdempotencyStore` |
| **Attempt** | One logical save in the WebApp; keeps its key while the save is retried | `IdempotencyAttempt` (WebApp) |
| **Verified user** | The OpenEMR user the gateway checked the session for and forwarded. The *acting provider* and the audit actor | `VerifiedActor` |
| **Audit entry** | Who did what to which record, field *names* only, in this service's own append-only table | `AuditEntry`, `IAuditLog` |
| **Outbox event** | A record of a change, written in the same transaction as the change | `*Event`, `clinical_care_outbox` |
| **Patient reference** | The `uuid` and `pid` of a patient this service has been told about, and nothing else | `PatientReference`, `clinical_care_patient_ref` |
| **Soft delete / flag** | Marking a row deleted in legacy's own way; nothing removed. For vitals and SOAP notes: `forms.deleted`. For clinical notes: `activity = 0` | `DeleteRecordUseCase`, `EfRecordDeleter` |
| **Hard delete** | Removing the row, as legacy does for problems and allergies | `DeleteIssueUseCase`, `EfIssueDeleter` |

## 2. Prohibited usages

| Do not say | Say | Because |
|---|---|---|
| "The patient" for anything held here | "the patient *reference*" | This context holds no demographics. If you need a name or a date of birth, ask Patient Management |
| "Encounter id" for the number | "encounter number" (or "uuid") | The row id is neither; legacy screens show the number |
| "Form" for a clinical fact | "vitals set", "SOAP note", "clinical note" | `forms` is legacy's registry table; "form" belongs to infrastructure |
| "Delete" without saying which | "flag as deleted" or "remove" | Vitals and notes are flagged and restorable in OpenEMR; problems and allergies are gone. The confirmation says which |
| "Update" for a vitals correction | "correct" | A correction replaces the measurements and keeps who recorded it and when |
| "Version" for the stamp *and* the hash | "concurrency token" and "content version" | They are different tokens with different `ETag` shapes |
| "Provider" for a person chosen in a form | "provider" only for the acting user, or "the provider named on the encounter" | The acting provider is the verified user; a body field never names it |

## 3. Where language and code disagree

These are real mismatches with legacy or between layers, each already decided. They are written down so nobody "fixes" one by accident.

| # | Disagreement | What the code does | Why |
|---|---|---|---|
| L1 | Legacy's `form_encounter.id` is the row id, `encounter` the number, `uuid` the public identity | The API exposes `uuid` and `number`; the row id never leaves infrastructure | Three identities, one meaning each |
| L2 | A SOAP note has no uuid | It is addressed as `/encounters/{encounterUuid}/soap-notes/{noteId}`, and named `soap:{id}` in audit rows and events | ADR 0073 |
| L3 | Legacy's `authorized` column on a new encounter holds the provider id | This service writes `1` | The column is a `tinyint`; legacy's non-strict mode truncates, this service's strict mode errors (finding F-CC-10) |
| L4 | A deleted clinical note leaves a "Clinical Notes Form" line in OpenEMR's encounter list | Same as legacy's own delete (`activity = 0`) | `21-permissions-and-delete.md` L8 |
| L5 | OpenEMR's dashboard can show deleted vitals as the latest | The flag is set exactly as legacy sets it; the dashboard's query ignores the form type | `21-permissions-and-delete.md` L7; a defect in OpenEMR's query |
| L6 | "No Known Allergies" appears after an allergy is deleted | `lists_touch` is left behind, as legacy leaves it | `23-delete-problems-and-allergies.md` L3 |
| L7 | FHIR `Observation` ids are not legacy's | `{vitalsUuid}-{slug}`, one per measurement | ADR 0079, decided by the project owner |
| L8 | Times: users think in local time | Every time is UTC, in the WebApp and in storage | ADR 0083, decided by the project owner |
