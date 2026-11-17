# Clinical Care, slice 1: Create Encounter

- **Status:** Designed first (playbook MIG-3, DSC-1), then built. Slice 1 is **built and compiling; its tests are written but have not been run** (section 10 states the verification level).
- **Milestone:** ROADMAP M5. Decisions: [ADR 0064](../architecture/0064-clinical-care-shares-the-legacy-database-and-coexists-with-legacy-encounter-writers.md).
- **Method:** playbook slice cycle (`docs/microservice-playbook/10-roadmap-and-milestones.md` §4), steps 1 and 2.
  PatientManagement is the structural reference (`docs/microservice-playbook/14-reference-implementation-map.md`).
  Legacy source read: `/home/mo/repos/openemr` at commit `944c21456`, which is **8.6.0-dev**. **The running stack is
  OpenEMR 8.0.0** (`version.php` in the container), so every file and line citation below is to the newer tree.
  Section 2's "Version skew" records which claims were re-checked against the running 8.0.0 code. The database
  shape is from `snapshots/db.sql`. Every claim below cites a file; anything not checked is in section 8.

## 1. Context in one paragraph

Clinical Care owns the clinical visit. Its first aggregate is the `Encounter`: one interaction between a
patient and a provider at a facility, with a class (ambulatory, inpatient, emergency, ...), a date, a
reason, and a provider. It holds only a **reference** to the patient (the patient's `uuid`, with `pid`
carried as a correlation key while legacy joins on it, design-set decision D1). It never copies
demographics. The slice creates an encounter and reads it back; it emits `EncounterCreated`.

## 2. Research notes (step 1)

### Legacy code paths that create an encounter (the writers, LEG-8)

| # | Writer | Evidence |
|---|---|---|
| 1 | Encounter form and REST API: `EncounterService::insertEncounter` | `src/Services/EncounterService.php:396`, called from `interface/forms/newpatient/save.php:159` |
| 2 | Appointment to encounter | `library/encounter_events.inc.php:200` |
| 3 | Document import | `controllers/C_Document.class.php:1505` |
| 4 | HL7 lab results (core) | `interface/orders/receive_hl7_results.inc.php:561` |
| 5 | HL7 results (DORN module) | `interface/modules/custom_modules/oe-module-dorn/src/ReceiveHl7Results.php:1513` |
| 6 | Group attendance | `interface/forms/group_attendance/functions.php:126` |

261 PHP files mention `form_encounter`; most only read it. Updates (`updateEncounter`, billing columns
`last_level_billed`, `stmt_count`, ...) have further writers that are **not** inventoried per column yet,
which is why slice 1 is create and read only (ADR 0064, decision 2).

### What `insertEncounter` actually does (`EncounterService.php:396-455`)

1. Validates (`EncounterValidator` insert context): `pc_catid` required; `class_code` required and must exist
   in list `_ActEncounterCode`; `puuid` required, a UUID, and present in `patient_data`.
2. `QueryUtils::generateId()` allocates the encounter **number** from the shared `sequences` counter
   (`src/Common/Database/QueryUtils.php:418`, ADODB `GenID("sequences")`).
3. Creates a `uuid` through `UuidRegistry` for table `form_encounter`, which also **inserts a row into
   `uuid_registry`** (`UuidRegistry.php:109-124`, `569-588`).
4. Defaults `date` when empty. The REST service uses `date("Y-m-d")` (midnight); the UI save script sets a
   full `Y-m-d H:i:s` (`save.php:55-63`).
5. Resolves `pid` from `patient_data` by uuid.
6. `INSERT INTO form_encounter`, then separately `addForm(...)` which `INSERT`s a `forms` row
   (`formdir = 'newpatient'`, `form_name = 'New Patient Encounter'`, `form_id` = the new `form_encounter.id`,
   `authorized` = provider id). **The two inserts are not in a transaction.**

Additional behaviour in the UI save script (`save.php`): `facility` (a denormalized *name*) is copied from
`facility.name`; `pos_code` defaults from `facility.pos_code` (`EncounterService.php:840`); a blank
`class_code` takes the `_ActEncounterCode` list default, or its first entry (`save.php:114-130`); the
provider defaults to the signed-in user; `encounter-types` is looked up to fill `encounter_type_code` and
`encounter_type_description`; issues are linked afterwards (`PatientIssuesService`).

### Soft delete

Deleting an encounter sets `forms.deleted = 1` on its `newpatient` row and leaves `form_encounter` alone
(`interface/patient_file/encounter/delete_form.php:56`). `EncounterService::getActiveEncounterByUuid`
(`:70-95`) treats such an encounter as gone, using `NOT EXISTS` because `forms` allows several `newpatient`
rows per encounter. Any read here must do the same.

### Version skew: the source I read is not the version that runs

All 9 files cited above differ between the 8.6.0-dev source tree and the 8.0.0 image the stack runs (40 to 360
differing lines each). I re-read the relevant code **inside the running container** on 2026-11-16. Result:

| Claim | In 8.0.0 |
|---|---|
| `insertEncounter` validates, draws the number, creates a uuid and registry row, inserts `form_encounter`, then `addForm`, with no transaction | **Holds.** Same steps; `addForm` gets `$data["provider_id"]` as `authorized` |
| Validator: `pc_catid`, `class_code` (list), `puuid` required | **Holds**, identical |
| UI save script: date mode, `getPosCode`, facility name, provider default, class default | **Holds** (different line numbers) |
| The counter is `update sequences set id=LAST_INSERT_ID(id+1)` | **Verified** in `adodb-mysqli.inc.php`, autocommitted |
| `uuid_registry` row per uuid, `table_name = 'form_encounter'`, `table_id = 'id'` | **Holds** |
| No `activity` filter on the class list or its validation | **Holds** |
| **Soft delete: `EncounterService` treats a flagged-deleted encounter as absent** (CC-9) | **Does not hold.** 8.0.0 has no `getActiveEncounterByUuid`, and `EncounterService` never reads `forms.deleted`. Only the UI filters (`delete_form.php`: `f.deleted = 0`). CC-9 is therefore stricter than 8.0.0's REST service and matches 8.0.0's **UI**, and matches 8.6's service |

Citations to line numbers in this document are to 8.6.0-dev. Do not rely on a line number without checking it against
the version being migrated.

### Schema facts that shape the design (`snapshots/db.sql`)

- `form_encounter.id` is the real `PRIMARY KEY`; the **business key** `encounter` has only a non-unique
  index `(pid, encounter)`; `uuid` has a real `UNIQUE KEY`. Public identity is the `uuid` (m2 inventory,
  anti-pattern 3 and 5). `pid` and `encounter` are correlation keys.
- No foreign keys anywhere. Nothing stops an orphan `forms` row or a bad `pid`; the adapter must validate.
- `sequences` is a one-column, one-row table (`id int unsigned`), shared by every legacy writer.
- `uuid_registry.uuid` is the `PRIMARY KEY`; it records `table_name` for each uuid.
- `_ActEncounterCode` holds 11 codes in the snapshot (ACUTE, AMB, EMER, FLD, HH, IMP, NONAC, OBSENC, PRENC, SS,
  VR); **AMB** (`Outpatient`) is the only one with `is_default = 1`. `list_options` is keyed `(list_id, option_id)`
  and has an `activity` flag.

## 3. Rule catalogue (step 2)

`CC-n` is the rule id. "Parity" means the legacy system does this. **(new)** means it differs and was
chosen here (KD-7).

| Id | Rule | Legacy evidence | Parity / new | Enforced in (planned) |
|---|---|---|---|---|
| CC-1 | The patient reference must name an existing patient. | `EncounterValidator.php` (`puuid` + `validateId`) | Parity (mechanism changes: `IPatientDirectory`, ADR 0064 #5) | `CreateEncounterUseCase` via port |
| CC-2 | A calendar category (`pc_catid`) is required and positive. | `EncounterValidator.php` (`required('pc_catid')`) | Parity. Legacy never checks the category exists; not added. | `EncounterCreation.Create` |
| CC-3 | The class code must be in `_ActEncounterCode`; if omitted, the list default is used, else its first entry. | `EncounterValidator.php`; `save.php:114-130` | Parity (UI path) | `EncounterCreation.Create` + `IEncounterCodeLists` |
| CC-4 | If no date is given, the encounter date is now (full timestamp, from an injected clock). | `save.php:55-63` (UI); `EncounterService.php` uses date only | Parity with UI path; REST path loses the time of day (**new**, minor) | `Encounter.Create` |
| CC-5 | The encounter number comes from the shared `sequences` counter, never `MAX+1`. | `QueryUtils.php:418` | Parity (required for coexistence, LEG-12) | `IEncounterNumberAllocator` adapter |
| CC-6 | A new `uuid` is stored on the row **and registered** in `uuid_registry` with `table_name = 'form_encounter'`. | `UuidRegistry.php:109-124` | Parity; M4's patient path does not do this (finding F-CC-3) | Adapter, same transaction |
| CC-7 | Every encounter also gets a `forms` row (`newpatient`, `form_id` = encounter row id), written in the **same transaction**. | `EncounterService.php:427-437`; `FormService.php:62-102` | Parity for content, with two deviations: atomicity is **(new)**, and `forms.authorized` is `1`, not the provider id legacy passes (F-CC-10) | Adapter (one transaction) |
| CC-8 | `facility` (name) is copied from the facility, and `pos_code` defaults from the facility, when not supplied. | `save.php:65,87-88`; `EncounterService.php:840` | Parity. Unknown non-zero facility is rejected, **(new)** (legacy stores a null name; decided, D-CC-3) | `EncounterCreation.Create`, fed by `IFacilityDirectory` |
| CC-9 | Reads treat an encounter whose `newpatient` forms row is flagged deleted as absent. | `EncounterService.php:70-95` | Parity with the 8.0.0 **UI** and the 8.6 service; stricter than 8.0.0's REST service (version skew, section 2) | Read adapter |
| CC-10 | The acting provider is recorded as `provider_id` and on the `forms` row (`user`, `groupname`, `authorized`). It must be a positive id. | `save.php:94-95` | Parity in content; source is caller-supplied until identity forwarding (F-CC-1). Rejecting `0` is **(new)**: legacy stores `0` when nobody is signed in. | `EncounterCreation.Create`; `EfEncounterRegistry` for the `forms` row |
| CC-11 | `EncounterCreated` is written to the outbox in the same transaction as the rows (EVT-1). | none (legacy dispatches in-process only) | **(new)** | `EfEncounterRegistry` |
| CC-12 | `reason` is at most 65,535 characters. | none (the column is a longtext) | **(new)** input bound (SEC-7) | `EncounterCreation.Create` |
| CC-13 | A place-of-service code is 1 to 99. | `pos_code` is a signed tinyint; CMS codes are two digits | **(new)**: legacy would raise a raw database error above 127 | `EncounterCreation.Create` |
| CC-14 | `sensitivity` is at most 30 characters. | `form_encounter.sensitivity` is varchar(30) | Parity (the column's own limit) | `EncounterCreation.Create` |
| CC-17 | An encounter or onset date is between 1900-01-01 and one year ahead of now. | none: legacy's `datetime` columns accept almost anything | **(new)** (review finding R-7) | `EncounterCreation.Create` |
| CC-15 | A list is one patient's, newest first **by date** (not by encounter number), and bounded: at most 100 per page, default 25. | `EncounterService::getMostRecentEncounterForPatient` (a late-entered old visit has a higher number); legacy lists are unbounded | Order is parity. The bound is **(new)** (API-6) | `PageRequest.Create`, `EfEncounterReader` |
| CC-16 | A row with no `uuid` is not addressable and is left out of lists. A half-filled legacy row (no date, class, number, or a pid matching no patient) is still read, not an error. | `UuidRegistry::populateAllMissingUuids` backfills missing uuids; DOM-9 | Tolerant read, parity-preserving | `EfEncounterReader` |

### Deferred or excluded, with the precondition (KD-8)

| Item | Status | Precondition to build |
|---|---|---|
| Update an encounter | Deferred | Per-column writer inventory (LEG-8), concurrency token (LEG-10), audit (SEC-9) |
| `encounter_type` lookup (code and description) | Deferred | A code-list port; low risk, next slice |
| Link issues to the encounter | Deferred | Problem list context exists (`issue_encounter`) |
| `parent_encounter_id`, billing and collection columns | Excluded here | Billing context (R8) |
| Soft delete | Deferred | Update path and audit first |
| `sensitivity` enforcement | Deferred, see F-CC-2 | Authorization policy port (SEC-5) |

## 4. Aggregates, value objects, ports (as built)

Mirrors PatientManagement (`Patient`, `PatientRegistration`, `IPatientRegistry`), with one deliberate difference:
identity (uuid and number) and the clock are **inputs** to the domain (DOM-7, DOM-8), where PatientManagement
calls `Guid.NewGuid()` and `Random.Shared` inside its aggregate.

- **Domain (built):** `Encounter` (aggregate root; no public constructor or setter; `AssignGeneratedId` is
  internal), `EncounterCreation` (validated value object; `Create(...)` collects **all** errors, DOM-2/3),
  `PatientReference(Uuid, Pid)`, `EncounterIdentity(Uuid, Number)`, `ClinicalCareValidationException`,
  `EncounterClassRule` (pure, cited, DOM-12), and the reference-data records `EncounterClassOption`,
  `FacilityOption`, `EncounterReferenceData` (no invariants, read tolerantly, DOM-9).
- **Application.Ports (built):** `IPatientDirectory` (interim stand-in), `IEncounterCodeLists`,
  `IFacilityDirectory`, `IEncounterIdentityAllocator`, `IEncounterRegistry`, `EncounterCreatedEvent`.
- **Application (built):** `CreateEncounterUseCase`, `CreateEncounterCommand`, `PatientNotFoundException`.
- **Infrastructure (built):** `ClinicalCareDbContext` (deliberately **no mapped entities**: every legacy table is
  written with explicit parameterized SQL so legacy's multi-table write stays visible in one adapter, LEG-2),
  `EfEncounterRegistry`, `EfEncounterIdentityAllocator`, `EfPatientDirectory` (stand-in), `EfEncounterCodeLists`,
  `EfFacilityDirectory`, `EventEnvelope`, `Rfc4122GuidConverter`.
- **Api and Composition (built):** the options, the shared-secret handler, `POST /api/v1/encounters`, OpenAPI.
- **Read side (built):** `Domain.ReadModels` (`EncounterView`, `EncounterPage`, `PageRequest`), `IEncounterReader`,
  `GetEncounterUseCase`, `ListPatientEncountersUseCase`, `EfEncounterReader`, `GET /api/v1/encounters/{uuid}` and
  `GET /api/v1/encounters?patientUuid=&limit=&offset=`.
- **Client (built):** `OpenEmrNext.ClinicalCare.Client` is NSwag-generated from the committed
  `OpenAPIs/ClinicalCare.json` (`scripts/dev/generate-clinicalcare-openapi.sh`); a test fails if the snapshot drifts
  from the live document.
- **Projects and tests:** `OpenEmrNext.ClinicalCare.{Domain, Application.Ports, Application, Infrastructure,
  Composition, Api}` and `{UnitTests, ArchitectureTests, IntegrationTests}` (playbook 03 §2, 14 §4).

## 5. Ownership matrix (Clinical Care rows)

| Table | Service does | Source of truth | Other writers | Target owner | Grants | Failure behaviour | Rollback |
|---|---|---|---|---|---|---|---|
| `form_encounter` | INSERT, SELECT | Shared (legacy still writes) | Six legacy writers (§2) | Clinical Care | SELECT, INSERT | Whole transaction rolls back | Delete the new rows by uuid; no schema change |
| `forms` | INSERT, SELECT | Shared | Every legacy form writer | Clinical Care (for `newpatient`) | SELECT, INSERT | Same transaction as encounter | As above |
| `sequences` | Increment | Shared | All legacy id generators | Shared kernel (legacy) | SELECT, UPDATE (ADR 0049 explains why SELECT) | A failed insert wastes a number; acceptable, legacy does too | None needed |
| `uuid_registry` | INSERT | Legacy | Every legacy uuid creator | Legacy | INSERT only | Same transaction | Delete by uuid |
| `facility`, `list_options` | SELECT | Legacy (Organization) | Legacy admin | Organization (R10) | SELECT on named columns only (`facility`: id, name, pos_code; `list_options`: list_id, option_id, title, seq, is_default) | 400 on unknown code or facility | None |
| `users`, `groups` | SELECT | Legacy (Organization) | Legacy admin | Organization (R10) | SELECT on `users(id, username)` and `groups(name, user)` only: `users` also holds a `password` column | `forms.user` is NULL for an unknown provider (F-CC-6) | None |
| `clinical_care_patient_ref` | INSERT, SELECT | Clinical Care (filled from `PatientRegistered`) | none | Clinical Care | owned | 404 patient not found | Drop table |
| `patient_data` | **none** (read once, as root, by the seed backfill) | Patient Management | PatientManagement | Patient Management | no grant (removed in slice 3) | n/a | n/a |
| `clinical_care_outbox` | INSERT, relay marks published | Clinical Care | none | Clinical Care | ALL on this table | Same transaction as encounter | Drop table |

## 6. Slice plan (playbook 13 §9)

```text
SLICE:        Create an encounter for a registered patient, and read it back
CONTEXT:      Clinical Care (new service)
WHY NOW:      Self-contained create with a clear parity test; creation is safe to share because the
              sequences counter is shared (ADR 0064). Update/billing are deferred (MIG-10).
LEGACY EVIDENCE: section 2
RULES:        CC-1 .. CC-11 (section 3)
AGGREGATES:   Encounter; value objects EncounterCreation, PatientReference, EncounterClass
PORTS:        section 4
USE CASES:    CreateEncounter, GetEncounter, ListPatientEncounters (all built)
DATA:         section 5; ids from `sequences`; uuid in uuid_registry; no concurrency token (no update)
API:          POST /api/v1/encounters (patientUuid in the body) -> 201 + Location. 400 ValidationProblem
              listing every error, 404 problem+json for an unknown patient. A flat prefix, not
              /api/v1/patients/{uuid}/encounters: the gateway already sends /api/v1/patients/** to
              PatientManagement, and one prefix must belong to one service. GET by uuid and a paginated
              GET /api/v1/encounters/{uuid} and GET /api/v1/encounters?patientUuid= (paginated, API-6) are built. Idempotency key (API-5) is NOT built: F-CC-8. No actor
              in the body once identity forwarding exists; until then providerId is in the body (F-CC-1).
EVENTS:       EncounterCreated (envelope with eventId, schemaVersion, correlationId), same transaction
SECURITY:     Gate per ADR 0055's mechanism (shared secret, no per-user check, standing decision), with
              ClinicalCare's OWN secret (API-16), attached by the gateway only on this cluster. Constant-time
              compare; 401 without it; no published port; deny-by-default group. Audit deferred (F-CC-1).
TESTS:        domain; architecture (layers, invariant); integration as the production user against a
              MariaDB container with legacy-shaped tables; pipeline (auth, problem details); OpenAPI
              snapshot; shadow comparison against the legacy creation; manual legacy-UI check (LEG-15)
DEPLOY:       seed script `scripts/seeds/clinical-care-db.sh` (outbox table + user + one commented grant each,
              column-level where a table holds data this service must not see); Dockerfile + .dockerignore;
              compose services `seed-clinical-care-db`, `clinical-care` (no published port); gateway route
              `/api/v1/encounters/**` with its own secret; `.env.example`, `new-env.sh` and CI carry the
              two new variables
DOCS:         ADR 0064 (done); this document; ROADMAP M5; ownership matrix (06 in patient-management
              design set); glossary; changelog; ADR index
EXIT:         POST through the gateway creates an encounter that opens in the legacy UI; the outbox row
              exists; all tests pass as the production principal; shadow comparison clean
ROLLBACK:     Remove the gateway route and the compose service; delete created rows by uuid; drop the
              outbox table; revoke the grants. No legacy schema was changed.
```

## 7. Findings register (new rows)

| Id | Finding | Sev | Effort | Evidence | Fix | Depends on | Status |
|---|---|---|---|---|---|---|---|
| F-CC-1 | The acting provider is caller-supplied (no verified identity), so `provider_id` and the `forms.user` are unverified. Same as M4's `RegisteredBy`. | H | L | ADR 0055 | Identity forwarding (SEC-4) | AuthFacade work | Open, tracked |
| F-CC-2 | `form_encounter.sensitivity` gates legacy access through ACLs. This service stores it but nothing enforces it on read. | H | M | `form_encounter` column; legacy ACL `sensitivities` | Authorization policy port (SEC-5) | F-CC-1 | Open, tracked |
| F-CC-3 | M4's `Patient.Register` uses `Guid.NewGuid()` and writes no `uuid_registry` row, unlike legacy. May be harmless, may break uuid-to-table lookups. | M | S | `Patient.cs:70`; `UuidRegistry.php:109-124` | Verify (section 8), then register uuids in both services | none | **Fixed in PatientManagement** for `patient_data`, `insurance_data` and `documents` (slice 3; `drive_uuid` is F-PM-2) |
| F-CC-4 | Legacy `insertEncounter` does two inserts with no transaction; a failure can leave an encounter with no `forms` row. This service writes both atomically. | L | S | `EncounterService.php:419-437` | Done by design (CC-7) | none | Designed out |
| F-CC-5 | `patient_data` is read directly as an interim stand-in. | M | S | ADR 0064 #5 | Slice 3 | Relay + backfill | **Fixed**: the stand-in is gone (slice 3, ADR 0066) |
| F-CC-6 | The acting provider id is not checked against `users`. An unknown id registers with `forms.user` NULL. | M | S | `EncounterRegistryTests.CC_7_an_unknown_provider...` | A staff directory port (design set R10, D6) | none | Open, tracked |
| F-CC-7 | **PatientManagement** grants `SELECT` on all of `users`, which includes a `password` column. Clinical Care limits itself to `id, username`. Found while writing this service's grants. | M | S | `scripts/seeds/patient-management-db.sh:69`; `users` DDL in `snapshots/db.sql` | Narrow to the columns the FHIR practitioner lookup reads | none | **Fixed**: narrowed to `(id, uuid)` (slice 3, ADR 0066) |
| F-CC-8 | Create is not idempotent (API-5): a client retrying a lost response creates a second encounter. PatientManagement has the same gap. | M | M | no idempotency table exists in either service | Idempotency-Key header plus an owned table | none | Open, tracked |
| F-CC-9 | PatientManagement draws its `sequences` number inside the transaction that inserts the row, holding the shared counter's row lock until commit and serializing every legacy writer behind it. Clinical Care allocates in its own short transaction instead (D-CC-2). | L | S | `EfAdvanceDirectiveRepository.cs` (`UPDATE sequences` inside `BeginTransactionAsync`) | Same approach there | none | **Fixed** in PatientManagement (slice 3, ADR 0066) |
| F-CC-10 | Legacy writes the provider id into `forms.authorized`, a `tinyint(4)`. Legacy runs with `sql_mode = ''` and silently truncates an id above 127; this service is strict, so the parity copy **failed** for id 999 (found by the integration tests). Every other legacy caller passes the 0/1 flag. | M | S | `EncounterRegistryTests.F_CC_10_...`; `Installer.class.php:1752` (`SET sql_mode = ''`) | **Fixed by deviation:** write `1` (same meaning for any real provider, never overflows) | none | **Fixed**, deviation recorded in CC-7 |
| F-CC-11 | The OpenAPI document marks every property of a positional request record `required`, including nullable ones, so a generated client must send explicit nulls. PatientManagement has the same behaviour. | L | M | `ClinicalCare.json` (`CreateEncounterRequest.required`) | Mark optional properties non-required in a schema transformer | none | Open, contract debt, shared with PatientManagement |

## 8. Not verified (listed, not hidden)

| Claim | Why unverified | How to verify | Done |
|---|---|---|---|
| A uuid missing from `uuid_registry` breaks nothing in legacy | Moot for this service (it writes the row); still relevant to PatientManagement (F-CC-3) | Create a patient through M4, then read it with legacy's `PatientService` and FHIR API | No |
| ADODB `GenID` is `UPDATE sequences SET id = LAST_INSERT_ID(id + 1)` | **Verified** in the running 8.0.0 image (`adodb-mysqli.inc.php`); concurrent allocation also tested (20 parallel, no duplicates) | n/a | **Yes** |
| A `forms` row written by us renders in the legacy encounter screen | **Partly verified.** Legacy's own `EncounterService::getEncounter` and `getPatientEncounterListWithCategories` read the row and list it (category "Office Visit"). **Not yet seen in the browser**: that needs an OpenEMR login | Sign in at `http://localhost:8300`, open the patient, open encounter 3 | **Pending login** |
| `forms.groupname` should be the user's `groups` entry, defaulting to `Default` | Reasoned from `authProvider` use and one legacy path that writes `'Default'` (`CdaTemplateImportDispose.php:791`); the login code that sets `authProvider` was not read | Read the login path; compare with a form created through the legacy UI | No |
| Legacy `datetime` columns hold the server's local time, not UTC (LEG-11) | Not checked | Create an encounter in legacy and compare `date` with the container clock and zone | No |
| `pos_code` and `facility` are present for the synthetic facility | Not queried | Query the snapshot's `facility` rows | No |
| The adapters, endpoint, seed script, compose service and gateway route work as written | **Verified 2026-11-16:** 77 integration tests pass; the stack was started; an encounter was created through the gateway and read back; the gateway stack test passes (section 11) | n/a | **Yes** |
| Legacy tolerates a `date` with time of day on REST-created encounters | Legacy's own list function returned the row; the UI rendering is still pending login | Open it in the UI | Pending login |

## 9. Decisions still open (small, default in brackets)

| Id | Question | Options | Default |
|---|---|---|---|
| D-CC-1 | Write `uuid_registry` rows? | yes / no | **Decided: yes** (parity, CC-6); `INSERT` grant on `uuid_registry` only |
| D-CC-2 | Where does the encounter number get allocated relative to the transaction? | inside the insert transaction / just before it | **Decided: just before, in its own short transaction**, so the shared counter's row lock is held for microseconds, not for the whole multi-table insert (F-CC-9). A failed insert skips a number, as in legacy. |
| D-CC-3 | Reject an unknown non-zero facility id? | reject / store as legacy does | **Decided: reject** (**new**, existence checks are a playbook P1) |

## 10. Build status and verification level (QLT-15)

| Step of the slice cycle | State |
|---|---|
| 1 Research, 2 Specify | Done (sections 2 to 3) |
| 3 Domain | **Built and tested:** 63 unit tests pass (domain, use cases, envelope, converter) |
| 4 Ports and use case | **Built and tested** against fakes (included in the 63) |
| 5 Adapter | **Built and tested:** integration tests pass against MariaDB 12.3.3 with the real `snapshots/db.sql` DDL and the **real seed script** creating the production principal the tests connect as |
| 6 API and contract | **Built and tested:** create, get and list; OpenAPI snapshot, NSwag `Client`, and a drift-guard test |
| 7 Deploy | **Built and run.** The stack was started with the new services; the seed script ran against the real OpenEMR database; the live grants match the design; the gateway route works; the gateway stack tests pass 8/8 |
| 8 Docs | This document, ADR 0064, ROADMAP, ADR index, changelog |
| 9 Review (six dimensions) | **Done 2026-11-17**: [04-layered-review.md](04-layered-review.md). Seven defects found and fixed, seven findings tracked |
| 10 Register | Findings F-CC-1 to F-CC-11 above |

**Test results (run 2026-11-16):** unit 63/63, architecture 23/23, integration 77/77. The first integration run was 74/76:
one failure was a test bug (a `{}` in a format string); the other was a real defect, F-CC-10, fixed above. The
integration tests are the evidence for the adapter, grants and HTTP behaviour; they do **not** prove what the legacy UI
shows. The browser half of LEG-15 is the one outstanding exit item (section 11).

## 11. End-to-end verification against the running stack (2026-11-16)

Run against the live Compose stack (OpenEMR 8.0.0, MariaDB 12.3.3), with the new `seed-clinical-care-db` and
`clinical-care` services and the recreated gateway.

| Check | Result |
|---|---|
| Seed script ran against the real `openemr` database; `SHOW GRANTS` for `clinical_care` | Matches the design: column-level on `users`, `groups`, `facility`, `list_options`, `patient_data`; `INSERT` only on `uuid_registry`; no `UPDATE`/`DELETE` on `form_encounter` or `forms` |
| `clinical-care` has no published host port | Confirmed: `localhost:8080` unreachable |
| `POST /api/v1/encounters` with `{}` through the gateway | **400** (not 401, not WebApp HTML): routed, and the gateway attached the secret |
| `POST /api/v1/encounters` for the existing patient, facility 3, provider 1 | **201**, encounter number **3** (the counter was at 2) |
| `GET /api/v1/encounters/{uuid}` and the list, through the gateway | Return the encounter; list total 1 |
| Rows in `form_encounter`, `forms`, `uuid_registry`, `clinical_care_outbox` | All four written; uuid stored in RFC 4122 byte order; `forms.user` = `admin`, group `Default`, `authorized` 1; event envelope has `eventId`, `schemaVersion` 1, `correlationId` |
| **Legacy's own `EncounterService::getEncounter(uuid)`** | Finds it; resolves `puuid` through `patient_data`; returns the right pid, class, reason, provider |
| **Legacy's `getPatientEncounterListWithCategories(pid)`** (what the patient screen calls) | Lists encounter 3, dated, category **"Office Visit"** |
| Legacy's `forms` query for the `newpatient` row | `deleted 0`, `authorized 1`, `user admin` |
| `uuid_registry` lookup | `form_encounter` |

The smoke-test encounter (uuid `01a1e63a-4ea0-7ae1-a846-039eaa3415da`, patient "BrowserTest Patient") is still in the
local database so it can be opened in the UI. It is synthetic data in a local dev database.

**Not done: the browser check.** Opening it in the legacy UI needs an OpenEMR sign-in, which an automated session
should not type into a third-party login page. To finish LEG-15: sign in at `http://localhost:8300`, open
"BrowserTest Patient", and open the encounter dated 2026-11-16.

## 12. Shadow comparison against legacy creation (2026-11-16)

The ROADMAP M5 bullet: legacy and new encounter creation must match for synthetic scenarios. Each scenario was created **twice on the live
stack**: once by OpenEMR 8.0.0's own `EncounterService::insertEncounter` (run in the container), once by `POST /api/v1/encounters`, for the same
patient, facility, provider and inputs. Every column of the resulting `form_encounter`, `forms` and `uuid_registry` rows was then compared, except
the ones that are allocated per row (`id`, `uuid`, `encounter`, `form_id`, `last_update`).

| Scenario | `form_encounter` | `forms` | `uuid_registry` |
|---|---|---|---|
| A: minimal (category and class only, no date, no facility) | 30 identical, **1 differs** (`date`) | 10 identical, **1 differs** (`date`) | 6 identical |
| B: facility 3, explicit date | 31 identical | 11 identical | 6 identical |
| C: facility, reason, onset date, sensitivity, class `EMER`, category 9 | 31 identical | 11 identical | 6 identical |

**142 columns identical, 2 differ, and both are the already-documented rule CC-4:** with no date supplied, legacy's REST service stamps
`date("Y-m-d")` (midnight) while this service stamps the time of day, as the legacy UI path does. Nothing is unexplained.

What this does **not** cover: provider 1 was used, so `forms.authorized` is `1` on both sides; with any other provider id legacy writes the id and
this service writes `1` (finding F-CC-10, a recorded deviation). The user and group were given to legacy explicitly (`admin`, `Default`), so they
show this service resolves the same values for that user, not that the lookup matches legacy's session-derived group for every user. Billing,
update and delete paths were not compared (they are not built).

