# Clinical Care slice: permission port and deleting records (M5g)

Decisions are in ADR 0076; this note is the slice's working record: what was built, what each test pins, and what is left.

## 1. What was built

| Piece | Where |
|---|---|
| Port `IAccessControl` and `AccessRight` (`AdminSuper`, `DeleteRecords`) | `Application.Ports/IAccessControl.cs` |
| Adapter reproducing legacy's `aclCheckCore` for one right | `Infrastructure/EfAccessControl.cs` |
| Port `IRecordDeleter`, `DeletableRecord`, `RecordDeletedEvent`, `DeletionOutcome` | `Application.Ports/RecordDeletionPorts.cs` |
| Use case `DeleteRecordUseCase` (permission, encounter, delete, refusal audit) and the three exceptions | `Application/DeleteRecordUseCase.cs`, `AccessDeniedException.cs` |
| Adapter: lock, verify version, flag, event, audit, one transaction | `Infrastructure/EfRecordDeleter.cs` |
| `DELETE` on vitals, SOAP notes, clinical notes; 204 / 403 / 404 / 412 / 428 | `Api/Endpoints/RecordDeletionEndpoints.cs` |
| Grants: `forms (deleted)`, notes `activity`, nine `gacl_*` tables read-only by column | `scripts/seeds/clinical-care-db.sh` |

## 2. Decisions

| # | Decision | Why |
|---|---|---|
| PD-1 | Vitals and SOAP notes are flagged on `forms.deleted`; a clinical note on its own `activity`. | What legacy's screens and every reader here already honour; nothing is removed. |
| PD-2 | The right is `admin/super`, as legacy's `delete_form.php` requires. | The user chose to mirror legacy. |
| PD-3 | The permission is read from legacy's own rules on every deletion, uncached. | An administrator's change in OpenEMR applies at once; deletions are rare. |
| PD-4 | 428 comes before 403, and 403 before any lookup, then 404, then 412. | A user who may not delete learns nothing about which records exist. |
| PD-5 | The version is verified under `FOR UPDATE`; the SOAP version is the content hash, the others the stamp. | A legacy edit made after the caller's read is never deleted unseen; simultaneous deletes succeed once. |
| PD-6 | Events and audit rows name the record and the encounter and carry no clinical text. | EVT-5, SEC-9. |
| PD-7 | A malformed `If-Match` that is present is 412, not 400 or 500; an absent or `*` one is 428. | Same contract as every other conditional write. |
| PD-8 | A SOAP note is named `soap:{id}` in the audit row and event (it has no uuid). | ADR 0073. |

## 3. Limitations

| # | Limitation | Why accepted |
|---|---|---|
| L1 | ~~Issues (problems, allergies) are not covered.~~ **Covered since M5n (ADR 0080):** removed outright, as legacy does. | Decided by the user |
| L2 | No undelete. The `forms.deleted` grant could set the flag back, and the code never does. | An administrator restores in OpenEMR. |
| L3 | The WebApp offers Delete to everyone and shows the 403 as "only an administrator can delete"; there is no "can I delete?" signal. The buttons are **compile-checked only**: no browser check and the WebApp has no automated tests. | The session does not carry rights; refine later. |
| L4 | Legacy's `return_value` (read, write, addonly) and its per-session cache are not reproduced. | Nothing here needs a narrowed right; a cache would serve revoked rights. |
| L5 | Rules scoped to an extended object (`gacl_axo_*`) never grant the general right. | Legacy uses them for per-record rules this service has no notion of. |
| L6 | Linked documents and `note_related_to` of a deleted clinical note are untouched. | Not modelled by this service yet (known gap). |
| L7 | **OpenEMR's patient dashboard can still show deleted vitals as the "most recent vitals".** Found in the legacy-UI check (Nov 18). Its summary card (`vitals_fragment.php`) joins `forms` to `form_vitals` on `form_id` alone, with no `formdir = 'vitals'`, and `form_id` is shared by every form type, so a vitals set whose id equals another form's id (here, four deleted sets whose ids 3 to 6 matched four `newpatient` rows of other patients) is counted as not deleted. The flag is set exactly as legacy's own delete sets it, so a delete made in OpenEMR shows the same. Encounter lists and the vitals forms hide the set correctly. | A defect in OpenEMR's query, outside this service. The confirmation says so rather than promising "no longer shown in OpenEMR". |
| L8 | A clinical note deleted here still leaves a "Clinical Notes Form" line in OpenEMR's encounter list. | OpenEMR's own delete only clears the note's `activity`, and its report filters on that; the encounter list does not. Same as a delete made in OpenEMR. |

## 4. Tests

| Layer | What it pins |
|---|---|
| Integration, `AccessControlTests` (real MariaDB, legacy's DDL, production principal) | Superuser holds every right; a group inside another inherits; what a group does not hold is not held; a direct grant is that user's alone; **a refusal beats an allow**; a disabled rule grants nothing; an extended-object rule does not grant the general right; unknown, empty, whitespace and null users hold nothing; only the `users` section matches; usernames compare as legacy's columns do; a hostile username is data; **a rule changed in legacy applies on the next question** |
| Integration, `RecordDeletionTests` (41, run for all three kinds) | Flag set, row kept, record gone from reads and lists; audit row and event written, no clinical text, actor is the verified user; **non-admin 403 and nothing changed**; 403 before 404 (no probing); a right removed in legacy stops the next delete; no or `*` `If-Match` is 428; stale version is 412 with the current `ETag`; malformed version is 412; reachable only through its own encounter; second delete is 404 with one event and one audit row; **six simultaneous deletes succeed once**; siblings untouched; deleted record cannot be corrected; 401 without secret or user |
| Integration, grants | The seven `users` columns and the nine `gacl_*` column sets are exactly readable and the rest denied; every `gacl_*` write denied; only `forms.deleted` and notes `activity` newly updatable, every other `forms` column and removing a row denied |
| Unit, `DeleteRecordUseCaseTests` (16) | Order of refusals; permission before the encounter is read; no origin or username is a refusal, not a crash; each kind's action and event type; the SOAP key; every refusal writes one failed audit row |
| Mutation | With the permission check removed and the version check removed, 11 tests fail (403, 412, no-probing, right-taken-away); both restored |
