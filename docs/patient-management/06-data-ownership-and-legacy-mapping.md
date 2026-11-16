# Patient Management: data ownership and legacy mapping

- **Status:** Draft for review
- **Perspective:** Target design. **[Built]** matches the code and database grants; **[Target]** is a proposal.
- **Part of:** the Patient Management design set (`README.md` in this folder)
- **Depends on:** docs 01 to 05. Follows the legacy-integration checklist in
  `docs/domain-driven-design.md` ("Legacy database integration"): tables read, tables written, update
  behavior, source of truth, mapping and normalization, concurrency, failure behavior, audit, rollback.

## 1. The integration model in one paragraph

Patient Management runs **inside** the legacy `openemr` database (ADR 0049) as its own least-privilege
user. It does not own the schema (`patient_data` is `ExcludeFromMigrations`); it owns **rows** it
creates, and one new table, the outbox. All translation between the legacy shape and the domain model
happens in `Infrastructure` (the anti-corruption layer). Legacy PHP keeps reading, and in places
writing, the same rows. That sharing is the central fact of this document.

## 2. Ownership matrix

"Source of truth" is who decides the value. "Writers" lists everything that can change it.

| Table | This service | Source of truth today | Other writers | Target owner |
|---|---|---|---|---|
| `patient_data` | Insert (registration), update (`dupscore`, directive flags) | Shared | **Legacy**: demographics edit screen, **legacy REST API `POST/PUT /api/patient`** (enabled in this stack), duplicate report | This service, once C2 (update) ships and legacy write paths are closed |
| `insurance_data` | Insert, update (swap) | Shared | Legacy insurance screens | This service |
| `documents` | Insert; read; **soft-retire is Target** | Shared | Legacy document manager (any category, including Advance Directive) | This service for the Advance Directive category only |
| `categories_to_documents` | Insert (`REPLACE`) | Shared | Legacy | Shared with legacy (generic) |
| `sequences` | **Update** (draw an id) | **Legacy** (shared counter) | Every legacy writer that allocates a non-auto id | Legacy; we only borrow ids |
| `patient_management_outbox` | Insert; (Target) update, purge | **This service** | none | This service |
| `categories`, `list_options`, `users` | Read only | Legacy / Organization | Legacy | Organization and Practice |
| `insurance_companies` | Not read yet; **[Target]** read | Legacy / Organization | Legacy | Organization and Practice |
| `employer_data`, `patient_history`, `contact*`, `person*` | **[Target]** | Legacy | Legacy | This service |
| Documents volume (files) | Write, read, **[Target]** report orphans | Shared | Legacy document manager | This service for directive files |

**Single-writer claim, corrected.** ADR 0049 retires the legacy *registration screen*. Legacy's
**REST API** (`POST /api/patient`) is a second insert path into `patient_data`, runs the same
`PatientService::insert`, and is **enabled** in this stack's compose file (`rest_api`, `rest_fhir_api`).
AuthFacade's BFF proxy exposes that API to the browser. So patients can still be created by legacy code
today. Doc 03 R1 is amended accordingly.

## 3. Per-table detail

### 3.1 `patient_data`

**Written by this service**

| Column | Domain member | Normalization | Notes |
|---|---|---|---|
| `uuid` | `Patient.Uuid` | `binary(16)` in **RFC 4122 byte order**, not .NET's `Guid.ToByteArray` order (`Rfc4122GuidConverter`) | Getting this wrong stores a different UUID than PHP would read back, and breaks legacy's own FHIR API. Tested. |
| `pid`, `pubpid` | `Pid`, `Pubpid` | `bigint`; `varchar` | See 5. |
| `fname`, `lname`, `sex` | names, `Sex` | plain strings | `sex` is free text in legacy. |
| `DOB` | `DateOfBirth` | `DateOnly` ↔ `date` | A zero or partial date on an old row **throws** on read through EF (see 3.1 reads). |
| `email`, `email_direct` | `Email`, `TrustedEmail` | domain `null` ↔ column `''` (`NOT NULL DEFAULT ''`) | Mapped by backing field, because EF skips converters on `null`. |
| `regdate`, `date` | `RegisteredAtUtc`, last updated | `datetime`; **UTC** | See 7 on time zones. |
| `created_by` | `RegisteredBy` | `bigint` = `users.id` | Caller-supplied today (doc 03, M2). |
| `dupscore` | derived cache | integer; `-1` declared unique, `-9` pending | Written by `EfDuplicateScorer`; also written by legacy's report. |
| `completed_ad`, `ad_reviewed`, `advance_directive_user_authenticator` | directive status | `''`/`YES`/`NO`; `datetime`; `bigint` | Written by `EfAdvanceDirectiveRepository.SetStatusAsync`. |

**Not written:** `updated_by` (legacy has it; this service leaves it at its default, so a record this
service created has **no last-modifier**), the address, phone, race, ethnicity, language, and most of the
table's 132 columns. They rely on **column defaults**: every `NOT NULL` column in legacy's
`patient_data` has one (checked against `sql/database.sql`). A column added later without a default
would make every registration fail.

**Read by this service:** the FHIR read model selects demographics, address, phones, and coded columns
by **raw SQL** (`EfPatientFhirRepository`), joins `users` for the provider's UUID, and loads the seven
`list_options` lists it needs **once per call**, not once per patient (a fixed N+1).

**Update behavior.** Two narrow `UPDATE … WHERE pid = …` statements (score; directive status). **No
optimistic concurrency token** today. A future `Patient.Update` must add one (doc 04, PT-11).

**Mapping to a future load of `Patient` through EF.** Registration only ever inserts, so a dirty legacy
row never reaches the entity. The moment a command **loads** a `Patient` (CMD-2, CMD-3), a zero `DOB`
or an unexpected value on a row legacy wrote will throw. The insurance repository already solved this
(read dates as `CAST(… AS CHAR(10))` and skip what does not parse). **Target:** load `Patient` through
the same tolerant raw-read pattern, and decide what a command does with a row that fails rule checks
(refuse with a clear error, never write back a "corrected" guess).

### 3.2 `insurance_data`

| Column | Domain member | Normalization |
|---|---|---|
| `pid` | `Pid` | |
| `type` | `Level` | `primary`/`secondary`/`tertiary`; anything else is skipped on typed reads |
| `provider` | `CompanyId` | **a `varchar` holding a number**; blank, `0`, or non-numeric means *no payer* |
| `policy_number`, `group_number`, `plan_name` | same | |
| `date`, `date_end` | `StartDate`, `EndDate` | written as `yyyy-MM-dd`; read as **text** (`CAST … AS CHAR(10)`) so zero and partial dates survive |
| `uuid` | `Uuid` | RFC 4122 bytes |

- `UNIQUE KEY (pid, type, date)` is the database's backstop for IC-1.
- **Swap** runs **four `UPDATE`s in one transaction** and parks the displaced policy on the sentinel
  date `9999-12-31` for a moment, so the unique key is never violated mid-swap. The sentinel is a
  persistence detail and never leaves `EfInsurancePolicyRepository`.
- **Compatibility risk to verify.** Legacy's own validation expects subscriber demographics and
  `accept_assignment`. A policy this service creates has none of them (deliberate scope reduction,
  `m4-domain-notes.md`). Whether legacy's insurance screen **opens, displays, and saves** such a row
  without complaint has not been exercised.
- **No `DELETE` and no row is ever removed.** The `DELETE`-free grant matches that.

### 3.3 `documents`, `categories_to_documents`, the file

**Insert**, in one transaction, after the file is written:

1. Draw an id: `UPDATE sequences SET id = LAST_INSERT_ID(id + 1)`, then read `LAST_INSERT_ID()` **on the
   same connection** (the transaction pins it; without that EF may use two pooled connections and read
   someone else's id, found against a real database).
2. `INSERT documents (id, uuid, type='file_url', size, date=NOW(), url, mimetype, owner, revision=NOW(),
   foreign_id=pid, docdate, hash, name, drive_uuid, storagemethod=0, path_depth=1, encounter_id=0)`.
3. Look up the "Advance Directive" category **by name** (not by a hard-coded id).
4. `REPLACE INTO categories_to_documents`.

| Concern | Detail |
|---|---|
| `documents.id` | **Not** auto-increment. Borrowed from legacy's single shared `sequences` row, so a still-running legacy writer never collides. **Coupling:** correct only while every writer uses the same `LAST_INSERT_ID(id + 1)` idiom. |
| `foreign_id` | The **patient number** (`pid`), not `patient_data.id`. |
| `hash` | SHA3-512 hex of the content. |
| `name` | `"{Directive type} - {original file name}"` so legacy classifies the type by keyword (AD-4). |
| File path | `{repository}/{pid}/{driveUuid}`, no extension. `url` holds a `file://` path. Reads reject any path outside the repository root. |
| Read filter | `WHERE deleted = 0` |
| `date`, `revision` | `NOW()` is the **database server's** time zone, not UTC (see 7). |

### 3.4 `sequences`

A single-row counter shared by many legacy tables. The grant is `SELECT, UPDATE`: **`SELECT` is
required** because MariaDB demands read privilege on any column named on the right-hand side of a
`SET`. A `UPDATE`-only grant failed against the real seeded user and was **missed by every automated
test**, because the test fixture connects with a more permissive user (`m4-domain-notes.md`, ADR 0061
section). Treat this table as **borrowed, not owned**: draw one id at a time, never write any other
value, never reset it.

### 3.5 `patient_management_outbox`

Created by the seed script (`CREATE TABLE IF NOT EXISTS`), **not** by a migration. Columns today: `id`,
`event_type`, `payload`, `occurred_at`, `published_at`. Doc 05 section 6 adds `event_id`,
`schema_version`, `subject`, `actor`, `correlation_id`, `causation_id`, `attempts`, `last_error`,
`next_attempt_at`.

**Finding G-5.** `CREATE TABLE IF NOT EXISTS` never **alters** an existing table, so those column
additions will silently not happen on any database that already has the table. The outbox needs real
schema migration (a migration step run at startup or by the seed container) before doc 05's design can
land.

## 4. Grants (least privilege)  [Built]

| Table | Granted | Needed | Note |
|---|---|---|---|
| `patient_data` | SELECT, INSERT, UPDATE | same | Correct |
| `insurance_data` | SELECT, INSERT, UPDATE | same | Correct; no DELETE, matches "never remove" |
| `documents` | SELECT, INSERT | same today | **[Target]** soft-retire (AD-7) needs `UPDATE` on `deleted` |
| `categories` | SELECT | SELECT | |
| `categories_to_documents` | SELECT, INSERT, DELETE | INSERT (+ DELETE only because `REPLACE` may delete a duplicate) | Acceptable; the comment in the seed script explains it |
| `sequences` | SELECT, UPDATE | same | See 3.4 |
| `users`, `list_options` | SELECT | SELECT | |
| `patient_management_outbox` | **ALL PRIVILEGES** | SELECT, INSERT, UPDATE, DELETE | **Too broad.** `ALL` includes `DROP`, `ALTER`, `INDEX`. Narrow it. |

Gaps to close: narrow the outbox grant; add the grants Target features need **at the same time** as
the feature; and **run the integration tests as this exact user** (finding G-1 below).

## 5. Identity allocation: a design risk

### 5.1 What happens today

`Patient.Register` sets `Pid = Random.Shared.NextInt64(1, long.MaxValue)` and `Pubpid = Pid.ToString()`.

| # | Problem | Why it matters |
|---|---|---|
| G-2a | **The chart number is a random number of up to 19 digits.** `pubpid` is "what front-desk staff see and say" (doc 02). A value such as `4821973650218837461` cannot reasonably be read aloud, written on a form, or typed. | Usability defect in the one identifier staff handle |
| G-2b | **Collision gives a raw `500`.** The `pid` unique key rejects a duplicate, `EnableRetryOnFailure` treats it as non-transient, and nothing retries with a new value. | Vanishingly unlikely (a 63-bit random value, so roughly 1 in 10¹⁹ for any two patients) but unhandled, and untestable (doc 04, 14.2) |
| G-2c | **Legacy's own allocator breaks.** `PatientService::getFreshPid()` is `SELECT MAX(pid)+1`. After one random 63-bit `pid` exists, the next legacy insert (REST API, import) computes a number at or beyond `bigint`'s ceiling and fails or wraps. | Because legacy's REST API is still enabled (section 2), this is reachable now |
| G-2d | **Pids are not dense or ordered.** Anything that sorts or ranges by `pid`, or assumes it roughly counts patients, behaves differently. | Reports, exports |

### 5.2 Options

| Option | How | For | Against |
|---|---|---|---|
| **A. Random + retry** | Keep random; retry on duplicate key | Smallest change | Fixes only G-2b. Leaves G-2a, G-2c, G-2d. |
| **B. Locked `MAX(pid)+1` inside the registration transaction** | `SELECT MAX(pid) … FOR UPDATE`, add one, insert, commit; retry on duplicate key as a safety net | Dense, short, readable; **identical to what legacy computes**, so both allocators agree; fixes all four | Briefly serializes concurrent registrations (acceptable at registration rates); still racy against a legacy writer **not** using the lock |
| **C. Dedicated counter row** | A one-row table advanced with `LAST_INSERT_ID(n+1)`, seeded from `MAX(pid)` | No scan; same idiom already used for `sequences` | Legacy's `MAX+1` would not know about it unless the counter and `MAX(pid)` stay in step |
| **D. MariaDB `SEQUENCE` object** | `NEXT VALUE FOR` | Native, fast | Needs `CREATE` privilege and a schema object legacy does not know about; same sync problem as C |

**Recommendation: B**, with the duplicate-key retry kept as a backstop and with `Pubpid` defaulting to
the new, short `Pid`. It is the only option that keeps this service and legacy computing **the same
next number from the same table**, which matters for as long as legacy can insert. It also needs no new
schema object. It changes where `Pid` is assigned (inside the transaction in Infrastructure, behind
`IPatientIdentifierAllocator`, doc 04 section 13), so the aggregate takes the number as a parameter
instead of generating it, which also fixes doc 04's 14.2. When legacy insert paths are closed, switch to
C or D without touching the domain.

**Decision needed.** Whether to change pid allocation is a behavior change to a built, tested slice.
Document it as an ADR before doing it.

## 6. Concurrency assumptions

| Resource | Assumption today | Holds? | Target |
|---|---|---|---|
| `patient_data` insert | Unique `pid`, unique `uuid` | Racy with legacy REST insert after G-2c | Option B |
| `patient_data` update | No conflicting writer | **False**: legacy edits | Conditional update on last updated (PT-11) |
| `dupscore` | One writer | **False**: legacy report "Mark Unique" and "Recompute" | Respect `-1`; see doc 03 R1 |
| `insurance_data` | Unique `(pid, type, date)` | Holds (database enforced) | Same |
| `sequences` | Every writer uses `LAST_INSERT_ID(id+1)` | Holds for legacy PHP; a bare `UPDATE … SET id = id+1` elsewhere would break it | Keep the idiom, never deviate |
| Document file path | `{pid}/{driveUuid}` unique | Holds (uuid) | Same |
| Outbox | One writer, **no reader** | n/a | Relay with `SKIP LOCKED` (doc 05) |

## 7. Time

| Place | Clock |
|---|---|
| `patient_data.regdate`, `date` | **UTC**, written by this service from `DateTime.UtcNow` |
| `documents.date`, `.revision` | **Database server local time**, from `NOW()` |
| Legacy PHP | Server local time (PHP `date()` / MySQL `NOW()`) |
| Outbox `occurred_at` | UTC |

If the database server is not set to UTC, the same table family holds **two clocks** and legacy
screens show registration times offset from legacy-created rows. In this compose stack both are
probably UTC, so it is invisible, but **no document states it and no test pins it**. **Target:** record
the rule (store UTC everywhere new; never `NOW()` in this service's SQL; pass the time in), and assert
the server time zone at startup.

## 8. Failure behavior

| Failure | Behavior | State |
|---|---|---|
| Transient database fault | Retried by the execution strategy; transactions re-run as a whole (ADR 0056) | Built |
| Unique violation on `pid` | `500` (G-2b) | Gap |
| Registration fails after commit in the *caller's* view (scoring) | `500` though created (doc 05, F-1) | Gap |
| Database fails during a directive upload | File deleted best effort; real error rethrown | Built |
| Process killed between file write and commit | **Orphan file**, nothing reconciles | Gap |
| Row exists but file missing (restored database, lost volume) | Download throws; no handling | Gap |
| Read of a row legacy wrote that breaks a rule | Insurance: skipped. Patient: not yet exercised (3.1) | Partly built |
| Outbox row never published | Sits forever (no relay) | Gap |

**Target:** a **reconciliation report** (not auto-fix) listing files with no row, rows with no file,
and unpublished outbox rows older than a threshold; and a structured error for the `pid` collision and
the missing file.

## 9. Audit

| Need | Today |
|---|---|
| Who created a patient | `created_by` (caller-supplied, unverified) |
| Who last changed a patient | **Nothing**: `updated_by` is never written |
| Who filed or reviewed a directive | `documents.owner` (caller-supplied); `advance_directive_user_authenticator` (caller-supplied) |
| Insurance changes | **Nothing recorded** |
| Reads of PHI (who looked at which chart) | **Nothing** |
| Legacy's own audit log | Legacy has a `log` table. **This service writes nothing to it.** Whether legacy's own REST and UI patient-creation paths write there was not verified; if they do, patients created here are **missing from legacy's audit trail**. |

**Target:** the outbox events are **not** the audit record (they are purged). Decide in doc 07:
a dedicated, append-only audit table owned by this service (who, what, which patient, when, from where),
written in the same transaction as the change, **plus** a decision on whether to also write legacy's
`log` for continuity while legacy screens remain in use. Reads of PHI need their own access log.

## 10. Concept-to-legacy transition

Following `docs/domain-driven-design.md` ("Data ownership transition").

| New concept | Legacy source | Treatment now | Next step | End state |
|---|---|---|---|---|
| `Patient` | `patient_data` | Read and insert through the adapter | Add conditional update; close legacy insert paths | Own store, ACL becomes a migration tool |
| `InsuranceCoverage` | `insurance_data` | Read and write through the adapter | Add end and correct; validate payer | Own store |
| `AdvanceDirectiveDocument` | `documents` (+ category link) | Insert and read; borrowed ids | Soft-retire; narrow the grant | Own store and own file storage |
| `AdvanceDirectiveStatus` | three columns of `patient_data` | Pass-through | Give it a model (doc 04, 7) | Own store |
| `PatientContacts` | `patient_data` columns **and** `contact_*` | Not built | Confirm which legacy populates (doc 04, 8.4) | Own store |
| `Person`, `PatientRelationships` | `person`, `contact_relation`, `person_patient_link` | Not built | Verify against a populated database | Own store |
| `EmploymentHistory` | `employer_data` | Not built | | Own store |
| Outbox | new table | Insert only | Relay, migration, narrower grant | Own store |
| Audit | none | None | New audit table | Own store |

The path is the one the guide prescribes: **read through an adapter, define one authoritative writer per
concern, introduce new persistence only when ownership is clear, reconcile, migrate writes before reads
when rollback is safe, retire legacy access after an observation period.** Nothing here proposes
moving data yet.

## 11. Migration and rollback

- **Schema.** This service never migrates legacy tables. The **only** change it makes to the database is
  the outbox (and the audit table, if adopted). Both need a real migration mechanism (G-5).
- **Rollback of this service.** Stop it. Legacy keeps working because it reads the same rows. Rows this
  service wrote are valid legacy rows; the only residue is outbox rows and files, both harmless to
  legacy. **This is the strongest property of the shared-database design and the thing to protect:
  every row written must remain a row legacy can read and edit.**
- **Rollback of a data change.** There is none. No delete and no destructive update exist, so a mistaken
  write is corrected by a new write, not undone. Keep it so (merge and retire are soft).
- **Backup and restore.** Files and database rows must be restored together; a database restored
  without the volume (or the reverse) produces the orphan and missing-file cases in section 8.

## 12. Findings

| ID | Finding | Severity |
|---|---|---|
| G-1 | **Integration tests do not use the real least-privilege database user.** The `sequences` grant gap was found only by running the deployed stack. Every future grant mistake will be too. | High (process) |
| G-2 | **Pid allocation** (section 5): 19-digit chart numbers, unhandled collision, incompatible with legacy's `MAX+1` | **High** |
| G-3 | **Legacy REST API is an enabled second insert path** into `patient_data`, so the single-writer claim is false today | Medium |
| G-4 | **Patient load through EF will throw on dirty legacy rows** the moment any update command exists | Medium (future) |
| G-5 | **Outbox schema cannot evolve** with `CREATE TABLE IF NOT EXISTS` | Medium, blocks doc 05 |
| G-6 | **Outbox grant is `ALL PRIVILEGES`** | Low |
| G-7 | **Two clocks** (UTC and server local) with no documented rule | Low to medium |
| G-8 | **No audit record of changes or reads; legacy `log` bypassed** | **High** (healthcare) |
| G-9 | **Orphan files and missing files** are not detected | Low to medium |
| G-10 | **Legacy compatibility of rows this service creates** (insurance without subscriber data, patients with `updated_by` empty) is unverified | Medium |
| G-11 | **`sequences` is a shared counter** touched by one idiom only | Low, keep disciplined |

## 13. Open questions

1. **Pid allocation:** option B? (section 5)
2. **Legacy REST API:** is it meant to stay on? If yes, G-3 is permanent until patient writes move
   behind this service. If no, disable it in compose.
3. **Legacy audit `log`:** write to it for continuity, or only to our own audit table?
4. **Do we verify G-10 now** by opening a created patient and a created policy in the legacy UI? It is a
   cheap manual check that could invalidate a lot of the design.
5. **Where do migrations live**, and who runs them (seed container, startup, a separate job)?
