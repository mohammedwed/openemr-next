# 0076. Deleting vitals, SOAP notes and clinical notes needs legacy's admin/super right, and is a flag, not a removal

- **Status:** Accepted
- **Date:** 2026-11-17
- **Builds on:** [0064](0064-clinical-care-shares-the-legacy-database-and-coexists-with-legacy-encounter-writers.md), [0068](0068-clinical-care-updates-are-conditional-on-the-version-the-caller-read.md), [0069](0069-clinical-care-audits-writes-to-its-own-append-only-table.md), [0075](0075-the-gateway-validates-the-session-and-forwards-the-verified-user.md)
- **Closes:** the "permission port, then soft delete" item that M5b had deferred until a verified user existed.

## Context

Until now Clinical Care could create and correct records but never delete one; the roadmap kept "delete" behind two things: a verified user (ADR 0075, done) and a permission port. The user decided on Nov 17 that **deleting should mirror legacy**: only the people legacy lets delete, and for the three record types where legacy's own screens honour a flag.

How legacy does it, read from its source rather than assumed:

- Vitals and SOAP notes: `interface/patient_file/encounter/delete_form.php` runs `UPDATE forms SET deleted = 1` on the encounter's registry row, after `AclMain::aclCheckCore('admin', 'super')`. The row in `form_vitals` / `form_soap` stays. Every legacy and Clinical Care reader already skips a `forms` row with `deleted <> 0`.
- A clinical note: its own `activity` column. Legacy's note screens (`new.php`, `report.php`) show only `activity = 1` notes, and this service's reader and updater already do the same.
- **Problems and allergies are not in this decision.** Legacy removes the `lists` row for those (`DELETE FROM lists`), and its issue screens (`stats_full.php`) list every row of the type with no `activity` filter, so a flag would hide an issue here but leave it showing in OpenEMR. That mismatch needs the user's decision; see "Not decided".

Legacy decides who may delete with phpgacl: `AclMain::aclCheckCore` reads the `gacl_*` tables. Those tables are the one place the answer lives and administrators edit them in OpenEMR's own screens, so this service must read them, not copy them.

## Decision

1. **A deletion is legacy's own flag, never a removed row.** Vitals and SOAP notes: `forms.deleted = 1` on the registry row of this encounter and patient. A clinical note: `form_clinical_notes.activity = 0`. The record stays in its table for legacy's history, and legacy hides it as it hides its own deletions. This service never sets either flag back (no undelete).
2. **Only a user who holds legacy's `admin/super` right may delete.** A new port, `IAccessControl.IsAllowedAsync(username, right)`, answers it; its adapter reproduces `aclCheckCore` with one parameterized query over nine `gacl_*` tables: the user is allowed when some enabled rule allows the right to them, directly or through a group they are in or that contains one they are in (the nested-set bounds), no enabled rule refuses it, and the rule names no extended object (`gacl_axo_*`, which legacy uses for per-record rules). A refusal wins. A superuser holds every right, as in legacy. An unknown user or an empty username holds nothing.
3. **No cache.** A right taken away in OpenEMR stops a deletion on the next request. This is one extra query per deletion, which is rare; unlike the session check (ADR 0075 #3) it is not on every request.
4. **The answers are ordered so permission reveals nothing:** 401 (not signed in) then 428 (no `If-Match`) then **403** (not allowed) then 404 (no such encounter or record, or already deleted) then 412 (the record changed after the caller read it). A user who may not delete is refused before anything is looked up, so a 403 never discloses which records exist. Every refusal after the permission check writes a `failed` audit row; the 403 does too.
5. **The deletion is conditional on the version the caller read** (`If-Match`, ADR 0068 and 0072): the stamp for vitals and clinical notes, the content hash for a SOAP note. The record is locked `FOR UPDATE` and the version verified inside the transaction, so a legacy edit between the read and the delete is a 412 carrying the current `ETag`, and two simultaneous deletions of one record succeed once.
6. **One transaction**: the flag, the outbox event (`VitalsDeleted`, `SoapNoteDeleted`, `ClinicalNoteDeleted`: identifiers and the encounter, never any clinical text) and the audit row (`vitals.delete`, `soap.delete`, `clinical_note.delete`), as every other write.
7. **Database grants** (the seed script, pinned by `LeastPrivilegeGrantTests`): `UPDATE (deleted)` on `forms` and `activity` added to the clinical notes `UPDATE` columns; and read-only access to exactly the columns the decision query uses on the nine `gacl_*` tables (`acl.id/allow/enabled`, `aco_map`/`aro_map` section and value, the two group maps, `aro.id/section_value/value`, `groups_aro_map`, `aro_groups.id/lft/rgt`). The rule notes, return values, display names and every write stay denied. The grant on `forms.deleted` could set a flag back to 0; the code never does, and the tests pin that no other `forms` column is updatable.
8. **API:** `DELETE /api/v1/encounters/{encounterUuid}/vitals/{vitalsUuid}`, `.../clinical-notes/{noteUuid}`, `.../soap-notes/{noteId}`; `204` on success. One use case and one adapter serve all three, because the operation is the same and only the flag and the version differ.

## Consequences

- Deleting is possible, and only as legacy allows it. The two UIs agree about what is deleted and who could delete it.
- The WebApp cannot know in advance whether the user may delete (the session does not carry rights), so it offers Delete and shows the 403 as "needs administrator rights". A "can delete" signal is a later refinement.
- The service now reads a security configuration it does not own. If phpgacl's tables change shape in a future OpenEMR, the query and the grant tests change in one adapter; `AccessControlTests` runs against the real DDL from the snapshot.
- **Not reproduced, on purpose:** legacy's `return_value` narrowing (`read`/`write`/`addonly`) and its per-session cache. Nothing here asks for a narrowed value.
- **Restoring** a deleted record is not offered; an administrator does it in OpenEMR.

## Not decided

- **Problems and allergies (issues).** ~~Waiting on the user.~~ **Decided Nov 17: hard delete, as legacy does** (ADR 0080), because legacy's issue screens do not hide an inactive issue and a flag would leave the two systems disagreeing.
