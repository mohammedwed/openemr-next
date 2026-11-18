# Clinical Care: bounded context

- **Part of:** the Clinical Care design set (`README.md` in this folder)
- **Code:** `src/Services/ClinicalCare` (nine projects, see doc 07 diagram D2)

## 1. Purpose

Clinical Care records and corrects **what happened at a visit**: the encounter itself, the measurements taken (vitals), the conditions and allergies noted (the problem and allergy lists), the vaccines given, and the notes written (SOAP and clinical notes). It does this *without taking the data away from OpenEMR*: the rows are legacy's, the legacy screens stay live, and this service is one more well-behaved writer beside them (ADR 0064).

That single fact shapes everything else in the context. Most of the model is about **coexisting** with other writers: writing only the columns it owns, refusing to overwrite a change it has not seen, and reading legacy rows that may be half-filled.

## 2. Who uses it

| Actor | What they do | How they reach it |
|---|---|---|
| **Clinician** (a signed-in OpenEMR user) | Records and corrects clinical facts; deletes with the administrator right | The WebApp, through the gateway; every request carries the verified user |
| **Administrator** (legacy's `admin/super` right) | Deletes records | Same; the right is read from legacy's own `gacl_*` rules on every delete |
| **FHIR client** (a signed-in session, today) | Reads five resources, searches by patient | `/fhir/...` through the gateway; read-only |
| **Patient Management** (a service) | Tells it a patient exists | `POST /internal/events` with a shared secret, from its outbox relay |
| **Legacy OpenEMR** (other writers, other readers) | Writes and reads the same rows | Not a caller: a neighbour on the same database |
| **Operator** | Runs retention and reads logs | Configuration (`Retention:*`), the hosted service |

## 3. Capabilities

Each one is built, tested and merged. The slice note is where it was researched and built.

| # | Capability | Slice note | ADR |
|---|---|---|---|
| C1 | Create an encounter for a registered patient | 01 | 0064 |
| C2 | Update the encounter's editable facts | 06 | 0068 |
| C3 | Record vitals (US or metric), with derived BMI; correct them | 02, 13 | 0065 |
| C4 | Record, update and delete problems | 08, 10, 23 | 0070, 0080 |
| C5 | Record, update and delete allergies | 09, 10, 23 | 0070, 0080 |
| C6 | Link a problem or allergy to an encounter, and mark it resolved at that visit | 11 | 0070 |
| C7 | Record and correct immunizations (an entry made in error is marked, not removed) | 14, 15 | 0072 |
| C8 | Record, correct and delete SOAP notes | 16, 17 | 0073 |
| C9 | Record, correct and delete clinical notes | 19 | 0074 |
| C10 | Offer the configured lists: encounter classes, facilities, calendar categories, providers, note types and categories, vaccine options | 18 | |
| C11 | Know which patients exist (consume `PatientRegistered`), without copying them | 03 | 0066 |
| C12 | Serve read-only FHIR R4: `Immunization`, `AllergyIntolerance`, `Condition`, `Encounter`, `Observation` (vital signs), with `_lastUpdated` search | 24, 25 | 0079 |
| C13 | Delete vitals, SOAP notes and clinical notes only with legacy's administrator right | 21 | 0076 |
| C14 | Accept an `Idempotency-Key` on creating requests; guard a SOAP note against its own retry | 22 | 0077 |
| C15 | Keep its own append-only audit trail; write an outbox event with every change | 05 | 0069 |

Cross-cutting, not capabilities: verified identity (ADR 0075), conditional writes (ADR 0068), UTC times (ADR 0083).

## 4. What it owns

- **The rules** for the facts above: what a valid encounter, vitals set, problem, allergy, immunization or note is (doc 04).
- **Five tables of its own** (doc 06): `clinical_care_outbox`, `clinical_care_audit`, `clinical_care_idempotency`, `clinical_care_patient_ref`, `clinical_care_processed_events`.
- **The columns it is granted** on legacy's tables (doc 06). It owns those *columns*, not the tables: a legacy screen may write the others.
- **Its public contracts:** the JSON API (`/api/v1`, OpenAPI document committed at `src/Services/ClinicalCare/OpenEmrNext.ClinicalCare.Client/OpenAPIs/ClinicalCare.json`) and the FHIR read API.

## 5. What it does not own

| Not its job | Whose | Why it matters here |
|---|---|---|
| A patient's demographics, insurance, directives | Patient Management | Clinical Care holds only `PatientReference` (`uuid`, `pid`). It never reads `patient_data` |
| Who a user is, whether a session is valid | AuthFacade and the gateway (ADR 0075) | It trusts the verified user the gateway forwards, and only with the gateway's secret |
| Medications, prescriptions, surgeries, devices, health concerns | Other future services | They share the `lists` table with problems and allergies; every statement here names its own `type`, enforced by an architecture test |
| Labs, billing, scheduling, documents | Other future services | Not started. Whole-encounter hard delete is out of scope for good (legacy's `deleter.php` reaches billing) |
| Editing an encounter's `sensitivity` | Undecided | Needs its own permission decision (legacy's sensitivities ACL) |
| Linked documents of a clinical note, `note_related_to` | Not modelled | A known gap |

## 6. Boundary rules

1. **Reference, never copy.** A patient is a `PatientReference`; an encounter that vitals belong to is an `EncounterReference`.
2. **Write only owned columns, only conditionally.** Every update names its columns in the database grant *and* in the code, and is `If-Match`-conditional.
3. **Tolerant reads, strict writes (DOM-9).** A half-filled legacy row can be read; it cannot be extended (vitals cannot attach to an encounter with no number or patient).
4. **One transaction per write:** the record, its registry rows, its audit row and its outbox event commit together or not at all.
5. **The acting user is always a verified user.** A request body can never name who acted.
6. **Times are UTC** (ADR 0083). The database server, both services and legacy's PHP default all run UTC, which is what makes that true.

## 7. Quality attributes that drove the design

| Attribute | How it shows |
|---|---|
| Coexistence with legacy | Column-level grants, conditional writes, tolerant reads, legacy's own flags for delete |
| Auditability | An audit row in the same transaction as every write, including refusals; field names, never values |
| Safe retry | `Idempotency-Key` on creates; a SOAP retry guard; `412` and `428` on stale or unconditional updates |
| Replaceability | Hexagonal layers enforced by architecture tests, so the shared database can be replaced behind the ports |
| Standards | FHIR R4 read API validated against US Core in tests |
