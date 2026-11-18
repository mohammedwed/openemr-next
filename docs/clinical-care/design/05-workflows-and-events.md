# Clinical Care: workflows and events

- **Part of:** the Clinical Care design set (`README.md` in this folder)
- **Reads with:** doc 04 (the rules these workflows apply) and diagrams D8 to D14 in doc 07

## 1. The request pipeline

Every JSON request, whatever it does, passes the same stations in the same order. The order is the design: it decides which error a caller sees first, and it keeps an unauthenticated caller from learning anything.

| # | Station | What it does | Refuses with |
|---|---|---|---|
| 1 | **Gateway** (outside this service) | Checks the browser's session with AuthFacade; forwards the verified user's OpenEMR uuid and the shared secret | 401 (no session), 503 (AuthFacade cannot say; fails closed) |
| 2 | **Shared-secret authentication** | Proves the request came from the gateway (`X-Internal-Gateway-Secret`) | 401 |
| 3 | **Verified-user middleware** | Resolves `X-Authenticated-User` to the legacy user; makes them the **acting provider** and audit actor | 401 (missing or malformed header); 403 (unknown or inactive user) |
| 4 | **Idempotency middleware** (`POST` with an `Idempotency-Key` only) | Claims the key by (user, key); a repeat gets the first answer | 400, 409, 413, 422 (doc 05 §4) |
| 5 | **Endpoint** | Binds the body, reads `If-Match`, calls one use case | 400 (bad body), 428 (conditional write with no usable `If-Match`) |
| 6 | **Use case** | Applies the rules; decides the answer from the outcomes the ports return | 403 (delete without the right), 404, 412, 400 (rule violations, all collected) |
| 7 | **Registry or updater** (one transaction) | Writes the record, its registry rows, the outbox event and the audit row; for a conditional write, verifies the version under `FOR UPDATE` first | a conflict outcome, never an exception for an expected refusal |

**Why the order is what it is.** A caller with no identity is told to sign in (401) *before* anything about their request is judged, so a bad body never leaks through ahead of the sign-in prompt. For a conditional write the answer order is: **401** (no identity), **428** (no usable `If-Match`), **403** (a delete without the right; before any lookup, so someone who may not delete learns nothing about which records exist), **404** (no such record), **412** (the version offered is stale), **400** (the new facts break a rule).

## 2. Commands

A command changes state. All are performed as the verified user; none lets a body name who acted.

| # | Command | Route | Conditional on | Writes (one transaction) | Event |
|---|---|---|---|---|---|
| CMD-1 | Create an encounter | `POST /encounters` | key (optional) | `form_encounter`, `forms` (`newpatient`), `uuid_registry`, outbox, audit; the number first, from `sequences` | `EncounterCreated` |
| CMD-2 | Update an encounter | `PUT /encounters/{uuid}` | `If-Match` (stamp) | nine owned columns, outbox, audit | `EncounterUpdated` |
| CMD-3 | Record vitals | `POST /encounters/{e}/vitals` | key | `form_vitals`, `forms`, `uuid_registry`, outbox, audit | `VitalsRecorded` |
| CMD-4 | Correct vitals | `PUT /encounters/{e}/vitals/{v}` | `If-Match` (stamp) | the measurement columns, outbox, audit | `VitalsCorrected` |
| CMD-5 | Delete vitals (flag) | `DELETE /encounters/{e}/vitals/{v}` | `If-Match`; the right | `forms.deleted = 1`, outbox, audit | `VitalsDeleted` |
| CMD-6 | Record a problem / allergy | `POST /problems`, `POST /allergies` | key | `lists`, `lists_touch`, `uuid_registry`, outbox, audit | `ProblemRecorded`, `AllergyRecorded` |
| CMD-7 | Update a problem / allergy (resolving is an update) | `PUT /problems/{u}`, `PUT /allergies/{u}` | `If-Match` (`modifydate`) | the owned columns, outbox, audit | `ProblemUpdated`, `AllergyUpdated` |
| CMD-8 | Delete a problem / allergy (**removal**) | `DELETE /problems/{u}`, `DELETE /allergies/{u}` | `If-Match`; the right | the row **and its links**, outbox, audit | `ProblemDeleted`, `AllergyDeleted` |
| CMD-9 | Link an issue to an encounter; mark it resolved there | `PUT /encounters/{e}/issues/{i}` | | `issue_encounter`, outbox, audit | `IssueLinkedToEncounter` |
| CMD-10 | Unlink | `DELETE /encounters/{e}/issues/{i}` | | the link row, outbox, audit | `IssueUnlinkedFromEncounter` |
| CMD-11 | Record / correct an immunization | `POST /immunizations`, `PUT /immunizations/{u}` | key; `If-Match` (content) | `immunizations`, outbox, audit. A mistake is corrected to *entered in error*, never removed | `ImmunizationRecorded`, `ImmunizationCorrected` |
| CMD-12 | Record / correct / delete a SOAP note | `POST`, `PUT`, `DELETE /encounters/{e}/soap-notes[/{id}]` | key; `If-Match` (content); the right | `form_soap`, `forms`; the flag on delete; outbox, audit; an `AttemptId` in the event | `SoapNoteRecorded`, `SoapNoteCorrected`, `SoapNoteDeleted` |
| CMD-13 | Record / correct / delete a clinical note | `POST`, `PUT`, `DELETE /encounters/{e}/clinical-notes[/{u}]` | key; `If-Match` (stamp); the right | its own parent `forms` row (id allocated under a lock), `form_clinical_notes`; `activity = 0` on delete; outbox, audit | `ClinicalNoteRecorded`, `ClinicalNoteCorrected`, `ClinicalNoteDeleted` |
| CMD-14 | Consume `PatientRegistered` | `POST /internal/events` | `eventId` | `clinical_care_patient_ref`, `clinical_care_processed_events`, in one transaction | none |
| CMD-15 | Purge expired bookkeeping | hosted service, every `Retention:IntervalHours` | | deletes old processed-event ids and outbox rows | none |

Deletes are **permissioned**: the right is legacy's `admin/super`, read from its `gacl_*` tables on every delete, uncached.

## 3. Queries

A query changes nothing and returns views.

| # | Query | Route | Notes |
|---|---|---|---|
| QRY-1 | Get / list encounters | `GET /encounters/{uuid}`, `GET /encounters?patientUuid=` | paged (1 to 100, default 25); `ETag` on the single read |
| QRY-2 | Get / list vitals | `GET /encounters/{e}/vitals[/{v}]` | US or metric; `ETag` per set |
| QRY-3 | Get / list problems, allergies | `GET /problems[/{u}]`, `GET /allergies[/{u}]` | by patient; `ETag` |
| QRY-4 | List issues addressed at an encounter | `GET /encounters/{e}/issues` | |
| QRY-5 | Get / list immunizations | `GET /immunizations[/{u}]` | |
| QRY-6 | Get / list SOAP and clinical notes | `GET /encounters/{e}/soap-notes`, `…/clinical-notes` | |
| QRY-7 | Lookups | `GET /lookups/…` | encounter classes, facilities, calendar categories, providers, note types, note categories, immunization options |
| QRY-8 | FHIR read and search | `GET /fhir/{Resource}[/{id}]` | `Immunization`, `AllergyIntolerance`, `Condition`, `Encounter`, `Observation`; `patient`, `_id`, `_count`, `_offset`, `_lastUpdated`; an unsupported parameter is a 400 |

## 4. Idempotency (`POST` only)

A creating request may carry an `Idempotency-Key` (1 to 64 letters, digits and `_ . : -`). Without one nothing changes.

| The request | The answer |
|---|---|
| Same key, same request, first one **finished** | The first answer again, marked `Idempotent-Replayed: true`; nothing is created |
| Same key, same request, first still **running** | 409, `Retry-After: 5` |
| Same key, **different** request (method, path, query or body differs) | 422 |
| Malformed key, or two keys | 400 |
| Body over 1 MB | 413 |

The key belongs to the verified user, so one user's key cannot replay another's answer. Only a 2xx answer is kept; a refused or failed request releases its key so a corrected retry can reuse it. Claiming is atomic by the table's primary key (a request inserts its claim with a fresh token, reads it back, and owns the key only if the token is its own). A claim not completed within five minutes may be taken over. Answers are kept 24 hours. A **SOAP note** also guards its own retry: the registry writes an `AttemptId` in the `SoapNoteRecorded` event in the same transaction, and a *re-run* of that transaction first looks for its own event.

## 5. The event catalogue

Every change writes one event row to `clinical_care_outbox` in the same transaction (EVT-1). An event names the record and the encounter and carries **no clinical text** (EVT-5, SEC-9).

| Event | Raised by | Names |
|---|---|---|
| `EncounterCreated`, `EncounterUpdated` | CMD-1, CMD-2 | encounter uuid, number, patient |
| `VitalsRecorded`, `VitalsCorrected`, `VitalsDeleted` | CMD-3 to CMD-5 | vitals uuid, encounter |
| `ProblemRecorded`, `ProblemUpdated`, `ProblemDeleted` | CMD-6 to CMD-8 | problem uuid, patient |
| `AllergyRecorded`, `AllergyUpdated`, `AllergyDeleted` | CMD-6 to CMD-8 | allergy uuid, patient |
| `IssueLinkedToEncounter`, `IssueUnlinkedFromEncounter` | CMD-9, CMD-10 | link, encounter, issue |
| `ImmunizationRecorded`, `ImmunizationCorrected` | CMD-11 | immunization uuid, patient |
| `SoapNoteRecorded`, `SoapNoteCorrected`, `SoapNoteDeleted` | CMD-12 | `soap:{id}`, encounter, `AttemptId` |
| `ClinicalNoteRecorded`, `ClinicalNoteCorrected`, `ClinicalNoteDeleted` | CMD-13 | note uuid, encounter |

**Consumed:** `PatientRegistered`, from Patient Management (CMD-14).

### What happens to the events today

- **Nothing consumes them.** Clinical Care has no relay of its own and no other service subscribes. They are written, in the right transaction, so the day a consumer exists the history is there.
- **Retention** (`Retention:*`, on by default): an event delivered is kept 30 days, an event **never** delivered 365 days, a processed-event id 30 days. A bounded purge runs in the background.
- **The relay that exists is Patient Management's**, which delivers *its* `PatientRegistered` outbox to this service (below). It is the model a Clinical Care relay would copy.

## 6. Consuming `PatientRegistered` (CMD-14)

| Step | What happens |
|---|---|
| 1 | Patient Management registers a patient and writes the event to *its* outbox in the registration's transaction |
| 2 | Its relay claims due rows in order, under a lease, and `POST`s each to `/internal/events` with `X-Internal-Events-Secret` (a secret of its own, not the gateway's) |
| 3 | Clinical Care checks the secret, then the message: an `eventId`, a patient uuid that is not empty, a positive `pid`. An implausible registration time is recorded as unknown rather than failing the event (a tolerant reader, review finding R-5) |
| 4 | In **one transaction**: the event id is recorded as processed, and the patient reference is stored. An event already processed is acknowledged and does nothing (EVT-3) |
| 5 | A patient already known with the same `pid` is left alone. A **different uuid** claiming a held `pid`, or the same uuid with a different `pid`, is a real conflict and fails loudly (EV-2) |
| 6 | The relay retries with back-off from 5 s doubling to 5 min, dead-letters after 8 attempts or on a permanent rejection (400, 422), and delivers in outbox id order |

**Consequence the screens show:** a patient registered a few seconds ago is *not yet known* to Clinical Care, which answers **404** for them until step 4 completes. The WebApp says "still being set up" and retries by itself instead of reporting an outage (Nov 18).

## 7. Workflows

| # | Workflow | Steps |
|---|---|---|
| WF-1 | **Register, then see the patient's records** | Patient Management registers the patient and publishes; the relay delivers; Clinical Care stores the reference; the patient page's Clinical Care sections load (or, in the first seconds, say "still being set up") |
| WF-2 | **Record a visit** | Create the encounter (CMD-1) with the verified user as provider; record vitals (CMD-3); add a SOAP note (CMD-12); link a problem addressed at the visit (CMD-9) |
| WF-3 | **Correct without overwriting someone else** | Read the record and its `ETag`; send the correction with `If-Match`; on 412 the screen reloads what is stored and the user decides |
| WF-4 | **Retry safely** | The WebApp starts an *attempt* on first save and keeps its key while the save is retried, so a lost reply cannot create a second record |
| WF-5 | **Delete** | Only with the right; confirmation says whether the record is flagged (and restorable in OpenEMR) or removed (and not); an unclear outcome (5xx, dropped connection) says so and re-reads the list |
| WF-6 | **A legacy user edits the same row** | Legacy writes the row; the next read here carries a new `ETag`; a stale write is refused with 412; nothing is silently overwritten |
