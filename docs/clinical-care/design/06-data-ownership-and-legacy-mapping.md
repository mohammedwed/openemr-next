# Clinical Care: data ownership and legacy mapping

- **Part of:** the Clinical Care design set (`README.md` in this folder)
- **Source of truth:** `scripts/seeds/clinical-care-db.sh`, which creates the database user, its grants and its own tables. This document describes that script; if they disagree, the script and the grant tests win.
- **Decision:** ADR 0064 (share the legacy database and coexist with its writers), ADR 0068 (conditional updates on owned columns)

## 1. The model

Clinical Care shares OpenEMR's database with legacy. The legacy screens stay live, so the tables stay legacy's. This service is one more writer, **granted only the privileges it needs**, and it holds a small set of tables of its own for what legacy has no place for (audit, outbox, idempotency, patient references).

Three rules follow, each enforced by the database and not only by code:

1. **Writes are column-level.** An update is granted on exactly the columns the service owns; a column it does not own cannot be changed even by a bug.
2. **Rows are filtered by code where a grant cannot say it.** `lists` holds every issue type; the grant cannot say "only problems", so every statement names its `type`, and an architecture test fails if one does not.
3. **Nothing is deleted except where legacy deletes.** `DELETE` is granted on `lists` (problems and allergies are removed outright, as legacy does) and on `issue_encounter` (links). Everything else is flagged.

## 2. Tables this service owns

All are created by the seed script. The service holds the privileges shown.

| Table | Holds | Privileges | Notes |
|---|---|---|---|
| `clinical_care_outbox` | One event per change, written in the change's transaction | all | Not relayed by anything yet; unpublished rows kept 365 days (doc 05) |
| `clinical_care_audit` | Who did what to which record: actor, verified flag, action, subject (uuid as text), outcome, correlation id, source address, field **names** | **`INSERT`, `SELECT` only** | Append-only *by privilege*: no `UPDATE`, no `DELETE`, and retention never touches it (ADR 0069). Refusals are audited too |
| `clinical_care_idempotency` | `(actor_id, idem_key)`, the request fingerprint (a SHA-256), a state, a claim token and the stored answer | all four | Primary key is what makes claiming atomic; rows older than 24 hours are swept as a side effect of claiming. Never the request body |
| `clinical_care_patient_ref` | `patient_uuid`, `pid` (unique), `source` (`event` or `backfill`) | all | A reference and nothing else: no demographics. Filled by `PatientRegistered`; a one-time backfill copies patients registered before events existed (runs as root at every compose up, `INSERT IGNORE`) |
| `clinical_care_processed_events` | `event_id`, `event_type`, `processed_at` | all | Written in the same transaction as the event's effect (EVT-3); ids older than 30 days are purged |

## 3. Legacy tables this service touches

`R` read, `I` insert, `U(cols)` update of exactly those columns, `D` delete. "Token" is what the write is conditional on.

| Table | What it is | Privileges | Token | Notes |
|---|---|---|---|---|
| `form_encounter` | An encounter | `R`, `I`, `U(date, onset_date, reason, class_code, pc_catid, facility_id, facility, pos_code, provider_id)` | stamp `last_update` (database-maintained) | Never identity (`id`, `uuid`, `pid`, `encounter`), billing, or `sensitivity` |
| `forms` | Legacy's registry row that makes a clinical form appear in its screens | `R`, `I`, `U(deleted)` | n/a | One `newpatient` row per encounter; one `vitals` / `soap` / `clinical_notes` row per note. `deleted` is the soft-delete flag, never set back |
| `form_vitals` | A vitals set | `R`, `I`, `U(weight, height, temperature, temp_method, bps, bpd, pulse, respiration, oxygen_saturation, waist_circ, head_circ, BMI, BMI_status, note)` | stamp `last_updated` | Has **no** encounter column: its link to an encounter is the `forms` row. Not who recorded it or when |
| `lists` | Every issue type in one table | `R`, `I`, `U(title, begdate, enddate, diagnosis, outcome, occurrence, referredby, comments, verification, reaction, severity_al)` (column lists), **`D` on the whole table** | stamp `modifydate` | The widest grant. Held back by code (every statement names `type`; an architecture test; deletes reach a row only by uuid and type) and pinned by tests that try to delete a medication |
| `issue_encounter` | A problem or allergy addressed at an encounter | `R`, `I`, `D` | none | Ids and a resolved flag, no clinical text |
| `lists_touch` | "This list has been addressed" marker | `R`, `I` | n/a | Left behind on delete, as legacy leaves it (shows as "No Known Allergies" in OpenEMR) |
| `immunizations` | A vaccine given | `R`, `I`, `U(` the owned columns plus `update_date` `)` | **content hash** | No usable stamp, so the hash of the owned columns is the version (ADR 0072). `update_date` has no default, so this service sets it. No `D`: a mistake is marked `added_erroneously` |
| `form_clinical_notes` | A clinical note | `R`, `I`, `U(date, code, codetext, description, clinical_notes_type, clinical_notes_category, activity)` | stamp `last_updated` | `activity = 0` is how legacy retires a note, so deleting is that update. Its own parent `forms` row, id allocated under a database lock (ADR 0074) |
| `form_soap` | A SOAP note | `R`, `I`, `U(subjective, objective, assessment, plan)` | **content hash** | Has no uuid, so a note is its form id within the encounter (ADR 0073). Not `pid` or `date` (legacy's own REST update rewrites them) |
| `uuid_registry` | Legacy's record of every uuid it hands out | `I` | n/a | This service adds its own, never reads or edits |
| `sequences` | The one shared id counter | `R`, `U` | n/a | `UPDATE … SET id = LAST_INSERT_ID(id + 1)` also needs `SELECT`: MariaDB requires read on any column on the right of a `SET`. This is what keeps encounter numbers unique across writers |

### Reference data (read-only, only the columns used)

| Table | Columns | For |
|---|---|---|
| `list_options` | `list_id, option_id, title, seq, is_default, notes, activity` | Encounter classes, note types and categories, vaccine options |
| `facility` | `id, name, pos_code, inactive, service_location` | The facility lookup; name and place of service copied onto an encounter |
| `openemr_postcalendar_categories` | `pc_catid, pc_catname, pc_cattype, pc_active, pc_seq, pc_constant_id` | The visit category lookup |
| `users` | **seven columns:** `id, username, uuid, active, fname, lname, authorized` | The verified user, the providers lookup, FHIR practitioner display names. Never a password, email, NPI or other credential or contact column |
| `groups` | `name, user` | The group written on a legacy `forms` row |
| nine `gacl_*` tables | only the columns the single decision query reads (`gacl_acl`: `id, allow, enabled`, and the mapping tables' ids and values) | `IAccessControl`: does this user hold `admin/super`? Nothing is writable; rule notes and section names stay unreadable |

### Deliberately not granted

- **`patient_data`: nothing.** An interim stand-in that read it was removed when events arrived (ADR 0064 #5, ADR 0066). Clinical Care learns of patients only from `PatientRegistered`.
- Any other `lists` type's columns, billing columns, and every `DELETE` not named above.

## 4. Concurrency tokens by table

Two kinds, never mixed (their `ETag`s have different shapes so one cannot be mistaken for the other):

| Kind | `ETag` looks like | Used for | Why |
|---|---|---|---|
| **Stamp** (`ConcurrencyToken`) | `"1a2b3c4d5e"` (the stamp's ticks in hex) | encounters, vitals, problems, allergies, clinical notes | The database maintains the stamp (`ON UPDATE CURRENT_TIMESTAMP`) on **every** real change by **every** writer, which is what makes it safe against legacy |
| **Content hash** (`ContentVersion`) | `"c-" + 32 hex digits` | immunizations, SOAP notes | The table has no usable stamp, so a hash of the owned columns changes when any writer changes any of them, and does not change for a column this service does not own |

The stamp has whole-second precision, so two writes inside one second are indistinguishable; that residual window is accepted and documented (ADR 0068).

## 5. How each kind of delete is done

| Record | Done by | Reversible | Why |
|---|---|---|---|
| Vitals, SOAP note | `forms.deleted = 1` | Yes, by an administrator in OpenEMR | What legacy's own delete does; every reader here honours it |
| Clinical note | `form_clinical_notes.activity = 0` | Yes | Legacy's retirement of a note |
| Problem, allergy | the `lists` row is removed, with its `issue_encounter` links | **No** | Legacy removes the row; the user chose to mirror it (ADR 0080) |
| Immunization | never removed; corrected to *entered in error* | Yes | The permission port gates real deletes; this slice has none |
| Encounter | never | n/a | Legacy's `deleter.php` reaches billing: out of scope for good |

Every delete needs a verified user holding legacy's `admin/super` right, read from `gacl_*` on each delete (a refusal rule beats an allow; a group inside another inherits).

## 6. Clocks

Every time this service writes is UTC, and the database server, both services and legacy's PHP default all run UTC (ADR 0083). That is what makes it correct to store the digits it is sent. Legacy's own `gbl_time_zone` setting is empty. A deployment that moved any of them off UTC would have to revisit this.

## 7. What legacy shows differently (found in the legacy-UI check, Nov 18)

None of these is caused by this service; they are what OpenEMR's own queries do with correctly written rows. Detail in `docs/clinical-care/21-permissions-and-delete.md` (L7, L8) and `23-delete-problems-and-allergies.md` (L3).

- OpenEMR's **dashboard can still show deleted vitals** as the latest: its summary query joins `forms` to `form_vitals` on `form_id` alone, so a set whose id equals another form's id counts as not deleted.
- **"No Known Allergies"** appears for a patient whose only allergy was deleted, because `lists_touch` is left behind.
- A **deleted clinical note** leaves a "Clinical Notes Form" line in the encounter list.
