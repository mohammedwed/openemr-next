# Clinical Care slice: deleting problems and allergies (M5n)

Decisions are in ADR 0080; this is the working record.

## 1. What was built

| Piece | Where |
|---|---|
| Port `IIssueDeleter`, `DeletableIssue`, `IssueDeletedEvent`, `IssueDeletionRequest` | `Application.Ports/IssueDeletionPorts.cs` |
| Use case: permission, find the issue, delete, audit refusals | `Application/DeleteIssueUseCase.cs` |
| Adapter: lock, verify the version, remove the links and the row, event, audit; one transaction | `Infrastructure/EfIssueDeleter.cs` |
| `DELETE /api/v1/problems/{uuid}`, `DELETE /api/v1/allergies/{uuid}` | `Api/Endpoints/RecordDeletionEndpoints.cs` |
| Grant `DELETE` on `lists` | `scripts/seeds/clinical-care-db.sh` |
| Delete buttons with a confirmation on the two patient panels | `PatientProblemsPanel`, `PatientAllergiesPanel` |

## 2. Decisions

| # | Decision | Why |
|---|---|---|
| ID-1 | The row and its `issue_encounter` links are removed, not flagged. | Legacy removes them, and its issue screens would still list a flagged row. |
| ID-2 | Same right (`admin/super`), same order of answers (401, 428, 403, 404, 412), same audit as every other deletion. | ADR 0076. |
| ID-3 | The row is reached by uuid **and** type only; another type is a 404 through either route. | `lists` holds every issue type. |
| ID-4 | The service's user gets table-level `DELETE` on `lists`. | MariaDB cannot grant it by column or by value; the restriction is the code and its tests. |
| ID-5 | The event carries the issue's uuid and the patient, no clinical text. | EVT-5; a consumer holding a copy must know to drop it. |
| ID-6 | `lists_medication` is not touched. | It belongs to medications. |

## 3. Limitations

| # | Limitation | Why accepted |
|---|---|---|
| L1 | A deleted issue cannot be restored, and its content is not recoverable from this service (the audit row holds none). | Legacy's behaviour, chosen by the user; the confirmation says so. |
| L2 | The widest grant on a shared table: this service could in principle delete a row of any type in `lists`. | Not grantable more narrowly; pinned by tests that try. |
| L3 | `uuid_registry` and `lists_touch` rows are left behind. **Seen in the legacy-UI check (Nov 18):** `lists_touch` is legacy's "this list has been reviewed" marker, so after an allergy is recorded and then deleted, OpenEMR's dashboard reads the empty, reviewed list as "No Known Allergies" where a patient who never had one shows "Nothing Recorded". | As legacy leaves them; a uuid is never reused. A delete made in OpenEMR leaves the same marker. |
| L4 | ~~The Delete buttons have not been seen in a browser.~~ **Seen in a browser on Nov 18** for problems and allergies: the confirmation says it cannot be restored, the row is gone from the database, and a `problem.delete` audit row is written. That check also found that every delete reported failure (the OpenAPI document declared 200 for a 204), now fixed. | |

## 4. Tests (the new ones run and green; the full suites not run on this change)

| Layer | What it pins |
|---|---|
| Integration, `IssueDeletionTests` (against the real DDL, as the production principal) | A superuser removes the issue **and its links** and it is gone; one audit row and one event, no clinical text, the patient named; a non-admin is refused (403) and nothing changes; **permission before lookup** (403 for a record that does not exist); no or `*` `If-Match` is 428; a stale version is 412 with the current `ETag`; deleting twice is 404 with one event; **six simultaneous deletes succeed once**; a problem is not reachable through the allergy route and the reverse; **a medication, a surgery and a device are never deleted even with their uuid known**; siblings and their links are untouched; a right removed in legacy stops the next deletion; 401 without secret or user |
| Integration, `The_adapter_itself_refuses...` (4) | The adapter, called directly with a uuid that exists under another type, returns not-found and deletes nothing, even with the right version. **Needed because the use case's reader already 404s first, so the HTTP tests never reach this filter**; with the filter removed all 4 fail |
| Integration, grants | `DELETE FROM lists` is now allowed (and `lists_touch` still is not), with the reason written in the test |
| Component (bUnit) | The two panels run the same six delete scenarios as the form panels, plus a confirmation that says it cannot be restored |
