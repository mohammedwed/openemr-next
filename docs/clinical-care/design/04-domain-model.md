# Clinical Care: domain model

- **Part of:** the Clinical Care design set (`README.md` in this folder)
- **Code:** `OpenEmrNext.ClinicalCare.Domain` (aggregates, value objects, rules), `.Domain.ReadModels` (views), `.Application.Ports` (what the domain needs from outside)

## 1. A thin domain, on purpose

Clinical Care's aggregates are **records of what was observed**. Legacy owns their lifecycle: legacy screens create, edit and delete the same rows, and nothing here has a workflow of its own (no approval, no signing, no state machine beyond "recorded, then corrected"). So the domain model does three jobs:

1. **Decide whether a fact is valid** (the field rules), once, for create and update alike.
2. **Say what a fact is made of**, so the infrastructure writes exactly those columns.
3. **Carry identity and version** so a write can be conditional.

That is why almost every aggregate is a sealed class with private setters, one factory (`Create` or `Record`), and the rules in a static class beside it. The behaviour worth testing is in the rules, not in the objects.

## 2. Aggregates

An **aggregate** here is one legacy row plus the registry rows legacy needs beside it. None holds another by object; they refer by value (`PatientReference`, `EncounterReference`, `IssueReference`).

| # | Aggregate | Identity | Made of | Version token | Removed by |
|---|---|---|---|---|---|
| A1 | `Encounter` | `uuid`; `Number` (shared counter) | class, calendar category, date, onset, reason, facility (id, name, place of service), provider, sensitivity | stamp (`last_update`) | never (out of scope for good) |
| A2 | `Vitals` | `uuid`; belongs to an `EncounterReference` | up to ten measurements, derived BMI and category, note | stamp (`last_updated`) | flag (`forms.deleted`) |
| A3 | `Problem` | `uuid` | title, begin and end date, diagnosis, outcome, occurrence, referred by, verification, comments | stamp (`modifydate`) | **removal** of the row |
| A4 | `Allergy` | `uuid` | title, begin and end date, diagnosis, reaction, severity, verification, comments | stamp (`modifydate`) | **removal** of the row |
| A5 | `EncounterIssueLink` | `uuid`; names an encounter and an issue | resolved-at-this-visit flag | none: a link is added, flagged resolved, or removed | removal of the link |
| A6 | `Immunization` | `uuid` | `ImmunizationFacts` (about 23 facts: date, vaccine, lot, route, site, status, reason, note, entered-in-error) | **content hash** (`c-…`) | never: marked entered in error |
| A7 | `SoapNote` | **form id within its encounter** (no uuid) | `SoapSections`: subjective, objective, assessment, plan | **content hash** | flag (`forms.deleted`) |
| A8 | `ClinicalNote` | `uuid`; belongs to an `EncounterReference` | `ClinicalNoteFacts`: date, type, category, description, derived code | stamp (`last_updated`) | flag (`activity = 0`) |

`Encounter` has no `Correct` method: an encounter is **updated** through `EncounterChange` (the same field rules as `EncounterCreation`), and a vitals set is **corrected** through `VitalsChange`. Problems and allergies use `ProblemChange` and `AllergyChange`.

### Why these boundaries

- **One row, one aggregate** because that is the unit a legacy writer changes and the unit a conditional write can protect.
- **Vitals and notes are not inside the encounter.** Legacy joins them through `forms` by encounter *number* and `pid`, and they are corrected, deleted and listed on their own. They hold an `EncounterReference` (uuid, number, patient) so legacy's join can be written without loading the encounter.
- **Problems and allergies belong to the patient, not the encounter** (their routes have no encounter in them). The *link* is its own aggregate, because it is made and unmade on its own and carries its own flag.

## 3. Value objects

| Value object | Holds | Invariant |
|---|---|---|
| `PatientReference` | `uuid`, `pid` | Uuid is not empty, pid is positive. Never demographics |
| `EncounterReference` | `uuid`, `number`, `PatientReference` | Number is positive and the patient is known; a half-filled legacy row can be *read* but not *extended* (DOM-9) |
| `IssueReference` | which issue a link names: its uuid, its row id and its patient | A link can be built only from an issue that belongs to the same patient as the encounter |
| `ConcurrencyToken` | the row's last-update stamp, whole seconds | Equality on the stamp; its `ETag` is the tick count in hex, strong; weak or unreadable tags never match |
| `ContentVersion` | SHA-256 (128 bits) of the owned columns, canonical text | Every part length-prefixed, a missing part marked, so two rows never share a text by concatenation. Its `ETag` starts `"c-` and can never be mistaken for a stamp's |
| `Quantity`, `MeasurementUnit`, `MeasurementKind`, `UnitSystem` | a number and the unit it was stated in | Which units a kind accepts; storage is always US units (ADR 0065) |
| `EncounterIdentity`, `VitalsIdentity`, `ProblemIdentity`, … | the identity an adapter allocated | Created only through `Create(...)`; the domain never invents an id |
| `EncounterCreation`, `EncounterChange`, `VitalsRecording`, `VitalsChange`, `ProblemRecording`, `ProblemChange`, … | the *facts* of a create or an update, validated | One owner per rule: create and update share the same field rules |
| `EncounterReferenceData`, `AllergyCodeData`, `IssueCodeData`, `ImmunizationCodeData`, `ClinicalNoteCodeData` | the configured lists a rule checks against | Passed in by the use case; the domain never reads a list |

## 4. The rules (what makes a fact valid)

Rules collect **every** violation and throw once (DOM-3): the caller sees all the problems, in a stable order that tests rely on. Messages are the same whether they come from the browser's dialog or the server.

Ids that are real code ids (`CC-n`, `CV-5`, `CV-6`, `CV-10`, `DOM-n`) can be searched for in the code and its tests. **`CV-1` to `CV-3` and `IS-1` to `IS-5` are labels this document gives to rules the code states without an id: search for the message text instead.**

### Encounter (`EncounterFieldRules`, `EncounterClassRule`)

| Rule | Statement |
|---|---|
| CC-3 | A class code, if given, must be in the configured list; if not given it is the one flagged default, else the first. With no list configured the encounter is refused |
| CC-2 | A calendar category is required (legacy's validator requires it) |
| CC-8 | A facility id is not negative, and a facility named must exist ("Facility N does not exist"); its name and default place of service are copied onto the encounter |
| CC-10 | The provider is identifiable: between 1 and the column's maximum. (Legacy stores 0 when nobody is signed in; this service does not.) |
| CC-12 | Reason is at most 65,535 characters |
| CC-13 | A place of service, if given, is 1 to 99 |
| CC-14 | Sensitivity is at most 30 characters; an update never touches it (F-CC-2) |
| CC-17 | The date is between 1900-01-01 and one year from now; the onset date likewise |

### Vitals (`VitalsFieldRules`, `BmiRule`, `UnitConversion`)

| Rule | Statement |
|---|---|
| CV-1 | At least one measurement is required |
| CV-2 | Each measurement is within its range: weight 0.1 to 1500 lb, height 5 to 120 in, temperature 50 to 120 °F, blood pressure and pulse up to 400, respiration up to 150, oxygen saturation up to 100, circumferences up to 200 in |
| CV-3 | A measurement is stated in a unit its kind accepts, converted to US units for storage, with six decimals |
| CV-5 | BMI is weight ÷ height² × 703, only when both are above zero, rounded to six decimals: legacy's own formula |
| CV-6 | BMI category uses legacy's thresholds and labels (strictly greater-than; a BMI of 10 or less has none) |
| CV-10 | A vitals set is never edited by a legacy-compatible *update*; it is **corrected** by replacing the measurement columns |
| DOM-9 | An encounter with no number or no known patient cannot take vitals |

### Issues (`IssueFieldRules`, `ProblemFieldRules`, `AllergyFieldRules`)

| Rule | Statement |
|---|---|
| IS-1 | A title is required and is 2 to 255 characters; comments at most 65,535; referred-by at most 255 (problems); verification at most 36 |
| IS-2 | A problem's begin date is required; begin and end dates are between 1900-01-01 and one year from now, and the end is never before the begin |
| IS-3 | A diagnosis, if given, is 2 to 255 characters written as `SYSTEM:code` pairs separated by semicolons (for example `ICD10:J45.909`) |
| IS-4 | A problem's outcome and occurrence, and an allergy's reaction, severity and verification, must be values in their configured lists ("… is not a known …"); a value longer than its column is a clean message, not a database error |
| IS-5 | The provider must identify the acting provider (as CC-10) |

### Immunization, SOAP note, clinical note, link

| Aggregate | Rules |
|---|---|
| `Immunization` | An administered date is required. The vaccine is identified by an immunization code *or* a CVX code. An amount needs a unit. Administered-by and ordering provider, if given, must identify a user. A linked encounter must have a number and the **same patient**. Text lengths match the columns |
| `SoapNote` | At least one of the four sections is required; each is at most 65,535 bytes |
| `ClinicalNote` | A note (description) of at least 2 characters, a type, and a date are required; type and category must be in their configured lists; the code and its text are derived from the type |
| `EncounterIssueLink` | The encounter has a number and a known patient, the issue belongs to **the same patient**, and the encounter number and the issue's row id fit the columns legacy's `issue_encounter` holds |

## 5. What keeps it true

Rules in a method can be bypassed by a careless caller, so the important ones are also enforced by something that cannot be.

| Invariant | Enforced by |
|---|---|
| A domain object cannot be put in an invalid state after it is built | `Has_no_publicly_settable_property` and `Has_no_public_constructor` architecture tests over every invariant-enforcing type (this is the test that failed `main` on Nov 18, when `PageRequest` gained an `init` accessor) |
| The domain depends on nothing else of this service | `Domain_references_none_of_this_services_own_assemblies`, `ReadModels_references_only_domain`, `Ports_references_only_domain_and_read_models`, `Application_does_not_reference_infrastructure_or_the_api`, `Infrastructure_does_not_reference_application_or_the_api`, `Composition_does_not_reference_the_api_or_aspnetcore` |
| This service never touches a medication, surgery or other issue type in `lists` | `Every_statement_that_touches_lists_also_names_the_issue_type`, and `The_check_would_notice_a_statement_that_forgets_the_filter` (the test of the test) |
| This service writes only the columns it owns | Column-level grants in the seed script, and grant tests that try to write a column it does not own |
| A write never overwrites a change it has not seen | The version is verified under `SELECT … FOR UPDATE` in the same transaction as the write; adapter-level tests call the adapter directly, because the use case checks first and would hide a missing check |
| A record never exists without its audit row | Same transaction; the audit insert is the last statement before commit |
| A delete is by legacy's own right | `IAccessControl` replicates legacy's `aclCheckCore` over the nine `gacl_*` tables (superuser first; direct and group rules; a refusal beats an allow) |
| A retried create is one record | The idempotency store claims the key by primary key; a SOAP note carries an `AttemptId` in its event so a re-run transaction finds its own earlier commit |

## 6. Read models

Reads return **views**, never aggregates: `EncounterView`, `VitalsView`, `ProblemView`, `AllergyView`, `ImmunizationView`, `SoapNoteView`, `ClinicalNoteView`, `EncounterIssueView`, plus the lookup views and `ImmunizationOptions`. A view is built tolerantly (a legacy row may be half-filled) and carries the row's version so the caller can send it back.

`PageRequest` bounds every list: a limit of 1 to 100 (default 25), an offset, and an optional `UpdatedWindow` (the FHIR `_lastUpdated` search). It is immutable: it has no public setter, which is why `Within(window)` builds a new request.

## 7. Ports (what the domain and use cases need from outside)

Grouped by job; all are interfaces in `Application.Ports`, implemented once in `Infrastructure`.

| Job | Ports |
|---|---|
| Write a fact | `IEncounterRegistry`, `IVitalsRegistry`, `IProblemRegistry`, `IAllergyRegistry`, `IEncounterIssueRegistry`, `IImmunizationRegistry`, `ISoapNoteRegistry`, `IClinicalNoteRegistry` |
| Correct or update a fact | `IEncounterUpdater`, `IVitalsUpdater`, `IProblemUpdater`, `IAllergyUpdater`, `IImmunizationUpdater`, `ISoapNoteUpdater`, `IClinicalNoteUpdater` |
| Delete | `IRecordDeleter` (flag), `IIssueDeleter` (remove) |
| Read | `IEncounterReader`, `IVitalsReader`, `IProblemReader`, `IAllergyReader`, `IImmunizationReader`, `ISoapNoteReader`, `IClinicalNoteReader`, `IEncounterIssueReader` |
| Allocate identity | `IEncounterIdentityAllocator` (the shared counter under a lock), and uuid allocators for the rest |
| Reference data | `IEncounterCodeLists`, `IIssueCodeLists`, `IAllergyCodeLists`, `IImmunizationCodeLists`, `IClinicalNoteCodeLists`, `IFacilityDirectory`, the lookups |
| People | `IPatientReferenceStore` (patients told about), `IPatientDirectory`, `IUserDirectory` (the verified user; names for FHIR) |
| Cross-cutting | `IAuditLog`, `IAccessControl`, `IIdempotencyStore`, `IRetentionStore` |

Every registry and updater returns an outcome (`UpdateOutcome`, `ClaimOutcome`, …) rather than throwing for an expected refusal, so a use case can decide the answer: not found, version conflict, access denied.

## 8. Domain events

Events are **facts about a change**, written to the outbox in the change's transaction: `EncounterCreated`, `EncounterUpdated`, `VitalsRecorded`, `VitalsCorrected`, `ProblemRecorded`, `ProblemUpdated`, `AllergyRecorded`, `AllergyUpdated`, `IssueLinkedToEncounter`, `IssueUnlinkedFromEncounter`, `ImmunizationRecorded`, `ImmunizationCorrected`, `SoapNoteRecorded`, `SoapNoteCorrected`, `ClinicalNoteRecorded`, `ClinicalNoteCorrected`, and the five deletions (`VitalsDeleted`, `SoapNoteDeleted`, `ClinicalNoteDeleted`, `ProblemDeleted`, `AllergyDeleted`). They name the record and the encounter and carry **no clinical text** (EVT-5, SEC-9). Doc 05 has the catalogue and says plainly that nothing consumes them yet.
