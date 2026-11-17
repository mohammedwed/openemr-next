# Clinical Care M5d slice 3b: record, read, list and correct clinical notes

- **Milestone:** ROADMAP M5d, slice 3b. **Decision:** [ADR 0074](../architecture/0074-clinical-notes-get-their-own-parent-form-allocated-under-a-database-lock.md). **Builds on** ADR 0068 (conditional updates), 0069 (audit) and the vitals pattern (a form table linked to its encounter through `forms`).
- **Endpoints:** `POST /api/v1/encounters/{encounterUuid}/clinical-notes`, `GET .../clinical-notes/{noteUuid}`, `GET .../clinical-notes` (paged), `PUT .../clinical-notes/{noteUuid}` with `If-Match`; `GET /api/v1/lookups/clinical-note-types` and `/clinical-note-categories`. **Status:** built; suites green (section 5).

## 1. What legacy has (OpenEMR 8.0.0, read from the running container; the table is empty in this stack)

`form_clinical_notes`: `id`, `form_id` (the **parent form**, shared by the notes of one form), `uuid`, `date` (a date), `pid`, `encounter` (**text**), `user`, `groupname`, `authorized`, `activity`, `code`, `codetext`, `description` (the note, `text`), `external_id`,
`clinical_notes_type`, `clinical_notes_category`, `note_related_to` (`text`), `last_updated` (**`ON UPDATE CURRENT_TIMESTAMP`**). It has a uuid and a real stamp, unlike SOAP notes. Types and categories are the lists `Clinical_Note_Type` (each option's `notes` holds its LOINC code) and `Clinical_Note_Category`.

| Writer | How | Notes |
|---|---|---|
| The clinical notes form | `ClinicalNotesService::saveArray` | A new parent is `MAX(form_id)+1` with no lock; `user` is set on **insert only**; `code` and `codetext` are copied from the chosen type by script; `note_related_to` is parsed out of the description; **a note left out of a resubmitted form is retired with `activity = 0`**, never deleted |
| Linked documents and procedure results | `clinical_notes_documents`, `clinical_notes_procedure_results` | Not read or written here |
| REST | none for notes (FHIR `DocumentReference` reads them) | |

## 2. Rules

| Id | Rule | Legacy source | Parity | Where |
|---|---|---|---|---|
| CN-1 | The note is required: at least 2 characters of substance, at most 65,535 **bytes** (`text`); kept exactly as written. | none (the form accepts anything) | **(new)** | `ClinicalNoteFacts` |
| CN-2 | The type is required and a configured option, matched exactly; its **LOINC code and label are derived from it**, so they cannot disagree. Any configured option is accepted, active or not (legacy's validation filters on activity nowhere); only the lookups offer active ones. | form script | Parity in effect | `ClinicalNoteFacts` |
| CN-3 | The category is optional; if given, a configured option. Widths of the option ids and the code are checked as clean messages, not database errors. | | **(new)** | `ClinicalNoteFacts` |
| CN-4 | The note's date is a calendar date between 1900 and tomorrow. Recording defaults it to today; a correction requires it, so it cannot silently re-date a note. | the form's date field | **(new)** | facts, request |
| CN-5 | **Each note gets its own parent form** (a `forms` row plus the note row), its `form_id` allocated under a named lock taken before the transaction (ADR 0074). | `MAX(form_id)+1`, unlocked | **(new)**, safer | `EfClinicalNoteRegistry` |
| CN-6 | A note is reached only through its own encounter and patient: its own `pid` and `encounter` columns **and** its parent's `forms` row must agree, it must be `activity = 1`, and the parent not deleted. | | **(new)**, stricter | reader, updater |
| CN-7 | `user` is the acting provider's username, set on insert only, as legacy does; group, `authorized = 1` and `activity = 1` as legacy's form writes them. | the session | Parity | registry |
| CN-8 | A correction replaces date, type (and the derived code and label), category and description, conditional on the row's stamp (`If-Match`, 428 and 412 as elsewhere). It writes **only** those columns: not who wrote it, group, `authorized`, **`activity`**, `form_id`, `note_related_to`, `external_id`. | | **(new)** | updater, grants |
| CN-9 | One transaction holds the parent form, the note, the `uuid_registry` row, the outbox row and the audit row; a failing audit insert rolls all of it back; a retry finds the note by uuid. Events and audit rows name **fields**, never text. | | **(new)**, EVT-1, EVT-5, SEC-9, OPS-9 | registry, updater |
| CN-10 | Nothing changed means nothing written, no event, no audit row. The audit actor is the named provider on a record and **unknown** on a correction. | | **(new)**, ADR 0069 #5 | use cases |

## 3. Known limitations

| # | Limitation | Why accepted |
|---|---|---|
| L1 | Linked documents and procedure results are neither shown nor changed. | Different aggregates; their tables are not granted |
| L2 | **`note_related_to` is left empty on a new note and untouched on a correction.** Legacy derives it by parsing the text for issue references; after a correction that changes the text it can be stale. | Not parsed here; documented gap |
| L3 | **Legacy's own `MAX(form_id)+1` is unlocked.** This service's writers are serialised, but a legacy writer running at the same instant can still take the same id. Narrowed, not closed. | Inherent to the shared table |
| L4 | Retiring a note (`activity = 0`) is not offered. | It is a delete; the permission port |
| L5 | A note with no uuid (one the form created before uuids, or any unbackfilled row) is not listed or addressable. | As for every record type (ADR 0070 #6) |
| L6 | Inactive notes are not read at all; legacy's own search returns them with their `activity`. | A retired note is, to a caller, gone |

## 4. Contract

`ClinicalNoteResponse` carries an `eTag` (also on list items); `If-Match` is declared on the PUT with 412 and 428. Two lookups (`clinical-note-types` with each type's LOINC code, and `clinical-note-categories`) return the **active** options in legacy's order. OpenAPI snapshot regenerated.
Grants: `SELECT, INSERT` on `form_clinical_notes` and `UPDATE` on exactly its six editable columns; `SELECT` on two more `list_options` columns (`notes`, `activity`); no `DELETE`.

## 5. Tests

| Layer | What it pins |
|---|---|
| Unit (domain, application) | Every rule with its exact messages; derived code and label; inactive options accepted; width guards; defaulted date; changed-field detection (a type change is one change); order of checks; no-op writes nothing; refusals audited; lookups offer active options only |
| Integration (adapter) | Round trip; the parent form and note rows legacy expects; event and audit with no text; **next form id after a legacy one**; **eight simultaneous writers get eight distinct parent form ids** (this found the snapshot-before-lock bug); the lock is released after success and failure; a failing audit rolls back the parent form too; retry by uuid; reach only through the right encounter and patient; retired, deleted-parent and disagreeing-encounter notes not found; duplicated parent and shared parents read once; correction leaves user, group, flags, parent form and related issues alone; stale stamp, legacy edit, other encounter or patient, retired: not applied |
| Integration (HTTP) | 201, 200, 400, 401, 404, 412, 428; the `ETag` round trip; the code follows the type; the order of checks; no-op; audit and event content; the lookups, and that what they offer is accepted |
| Integration (grants) | Update allowed on exactly the six columns; denied on user, group, flags, `activity`, ids, parent form id, related issues, external id; the option columns readable are exactly those granted |
