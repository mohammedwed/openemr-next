# Clinical Care M5c slice 1: record, get and list problems

- **Milestone:** ROADMAP M5c, slice 1. **Decisions:** [ADR 0070](../architecture/0070-clinical-care-owns-the-problem-and-allergy-issue-types-in-the-shared-lists-table.md) (ownership of the shared `lists` table), [ADR 0068](../architecture/0068-clinical-care-updates-are-conditional-on-last-update-and-touch-only-owned-columns.md) (the `ETag`), [ADR 0069](../architecture/0069-clinical-care-audits-writes-to-its-own-append-only-table.md) (audit). **Research:** [07](07-problems-and-allergies-research.md).
- **Endpoints:** `POST /api/v1/problems`, `GET /api/v1/problems/{uuid}`, `GET /api/v1/problems?patientUuid=`. **Status:** built; tests in section 6.

## 1. What it does

Records a problem on a registered patient's list, reads one back with an `ETag`, and lists a patient's problems newest first, one bounded page at a time.
A problem is a row of legacy's `lists` table with `type = 'medical_problem'`; this service owns exactly that type (ADR 0070).

| Call | Outcomes |
|---|---|
| `POST /problems` | **201** + `Location`; **400** (any rule fails, all violations listed together); **401**; **404** (no such patient) |
| `GET /problems/{uuid}` | **200** + `ETag` (the row's `modifydate`); **404** (no such problem, **or the uuid belongs to a row of another issue type**) |
| `GET /problems?patientUuid=&limit=&offset=` | **200** (items, total, limit, offset); **400** (no patient, or a page out of bounds); **404** (no such patient) |

## 2. Rules

| Id | Rule | Legacy source | Parity | Where |
|---|---|---|---|---|
| PB-1 | A title is required, 2 to 255 characters after trimming. | `ConditionValidator` (`lengthBetween(2, 255)`) | Parity; trimming is **(new)** | `ProblemRecording.Create` |
| PB-2 | A begin date is required, between 1900-01-01 and a year from now. | `ConditionValidator` requires `begdate`; the range is CC-17 | Required: parity. Range: **(new)** | `ProblemRecording.Create` |
| PB-3 | An end date is optional, in the same range, and not before the begin date. | none | **(new)** | `ProblemRecording.Create` |
| PB-4 | A diagnosis is optional; if given it is 2 to 255 characters of `SYSTEM:code` pairs separated by semicolons. | validator: `lengthBetween(2, 255)`; the pair shape is what legacy writes | Length: parity. Shape: **(new)** | `ProblemRecording.Create` |
| PB-5 | `outcome` is a code of the configured `outcome` list; `0` (unassigned) is always allowed. | the column is an `int` with the list's `option_id`s | Parity with the list; the check is **(new)** | `ProblemRecording.Create`, `EfIssueCodeLists` |
| PB-6 | `occurrence` is a code of the configured `occurrence` list; `0` (unknown) is always allowed. | as PB-5 | as PB-5 | as PB-5 |
| PB-7 | `referredby` is at most 255 characters. | `varchar(255)` | Parity (the column's own limit) | `ProblemRecording.Create` |
| PB-8 | `comments` is at most 65,535 characters. | a `longtext` | **(new)** input bound (SEC-7) | `ProblemRecording.Create` |
| PB-9 | The acting provider is a positive id within the column's range. | the session's user | Parity in content; caller-supplied until identity forwarding (F-CC-1) | `ProblemRecording.Create` |
| PB-10 | `lists.date` is the instant of recording (`NOW()`), **not** the issue screen's `date("Y-m-d H:m:s")`, which writes the month into the minutes. | `PatientIssuesService` path vs `ConditionService` path | **Deliberate deviation**: the REST path wins where legacy's two paths disagree (ADR 0070 #5) | `EfProblemRegistry` |
| PB-11 | The row is written with `type = 'medical_problem'`, `activity = 1`, `pid`, and the acting provider's username and group as `user` and `groupname`. | both paths | Parity | `EfProblemRegistry` |
| PB-12 | The uuid is assigned at insert and registered in `uuid_registry`. | REST path (the issue screen assigns none) | Parity with REST | `EfProblemRegistry` |
| PB-13 | A `lists_touch` row `(pid, 'medical_problem')` is inserted if none exists. | `setListTouch` in the issue screen | Parity with the UI: legacy's summary reads it as `listTouched` | `EfProblemRegistry` |
| PB-14 | One transaction holds the row, the `uuid_registry` and `lists_touch` rows, the `ProblemRecorded` event and the audit row. | none (legacy writes without a transaction) | **(new)**, EVT-1, SEC-9 | `EfProblemRegistry` |
| PB-15 | The event carries ids and the instant only: no title, diagnosis or comment. | none | **(new)**, EVT-5 | `ProblemRecordedEvent` |
| PB-16 | **Every statement that touches `lists` filters on `type = 'medical_problem'`.** A medication, allergy, surgery or health concern is never a problem, even by its exact uuid. | the issue screen is generic over types | **(new)**, ADR 0070 #2 | `EfProblemRegistry`, `EfProblemReader` |
| PB-17 | A row with no `uuid` is not addressable and is left out of lists and totals. | the issue screen assigns no uuid until some service backfills it | Tolerant read (DOM-9); accepted gap, ADR 0070 #6 | `EfProblemReader` |
| PB-18 | A list is one patient's, newest first by begin date then row id, and bounded: at most 100 a page, default 25. | legacy lists are unbounded | Order: parity in spirit. Bound: **(new)** (API-6) | `PageRequest`, `EfProblemReader` |
| PB-19 | A refused request (unknown patient, validation) leaves a `failed` audit row naming the patient asked about, and writes nothing else. | none | **(new)**, ADR 0069 #4 | `RecordProblemUseCase` |
| PB-20 | A half-filled legacy row (no title, no begin date, an unknown `pid`) is still read, not an error. | other writers leave such rows | Tolerant read (DOM-9, LEG-4) | `EfProblemReader` |

## 3. Why the grants are column-level

`lists` holds every issue type, so a table-level grant would let this service read a medication's `udi` or a surgery's `injury_*` columns and write
any of them. The seed script grants `SELECT` on the sixteen columns a problem uses and `INSERT` on fifteen, and `SELECT, INSERT` on `lists_touch`.
There is no `UPDATE` or `DELETE`: updating arrives with slice 3, and delete waits for the permission port. **A grant cannot restrict rows**, so which rows
are touched is PB-16, a rule in the code that the tests attack from several sides (another type by exact uuid, other patients, uuid-less rows).

## 4. Known limitations (stated, not hidden)

| # | Limitation | Why accepted |
|---|---|---|
| L1 | **A problem created in the issue screen is invisible here until legacy gives it a uuid** (PB-17). | The issue screen assigns none; legacy backfills when any issue service is next constructed. Backfilling here would mean a write on a read |
| L2 | **Rows created by the issue screen keep the wrong minutes** in `date` (PB-10). | That is legacy's bug; this service does not reproduce it, and does not repair rows it did not write |
| L3 | **The actor is the provider the caller named,** flagged unverified. | No verified identity until Phase 1b |
| L4 | **No update, no resolve, no delete yet.** | Slice 3 (resolving is an update) and the permission port |
| L5 | **`diagnosis` codes are shape-checked only,** not looked up in a code system. | Q7: a terminology service is a later decision |
| L6 | **Allergies, medications, surgeries, dental issues and health concerns are not offered.** | Allergies are slice 2; the rest have no owner yet |

## 5. Not built here, with the precondition (KD-8)

| Item | Precondition |
|---|---|
| Update, resolve | Slice 3 (`modifydate` as the token, as for encounters) |
| Link a problem to an encounter (`issue_encounter`) | Slice 4 |
| Delete / inactivate | The permission port (phase 3b) |
| Filter by active or resolved | A need; the list returns both, with `endDate` visible |
| Medication detail (`lists_medication`) | A Pharmacy context |

## 6. Tests

| Layer | What it pins |
|---|---|
| Unit (domain) | `ProblemRecording`: every rule PB-1 to PB-9, all violations together, whole-second dates; `Problem`: identity, recorded-at, the id assigned once |
| Unit (application) | The use case against fakes: what reaches the registry, the audit row, no clinical content in the event, refusals audited, nothing allocated on refusal; the read use cases and their bound-before-lookup order |
| Integration (adapters) | Legacy-shaped row, the `date` rule, user and group, uuid registration, `lists_touch` idempotence, retry-once, no partial write, **no commit without the audit row**, other types and patients untouched; the reader against other types, other patients, uuid-less, half-filled and orphan rows, ordering and paging; the code lists; the column-level grants (readable, unreadable, uninsertable, unchangeable) |
| Integration (HTTP) | 201, 400, 401, 404, the `ETag` and its change under another writer, **another issue type's uuid is a 404**, lists exclude other types and patients and uuid-less rows, audit with the forwarded address |
