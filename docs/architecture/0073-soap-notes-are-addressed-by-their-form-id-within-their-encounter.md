# 0073. SOAP notes are addressed by their form id within their encounter

- **Status:** Accepted
- **Date:** 2026-11-17
- **Builds on:** [0072](0072-immunizations-are-versioned-by-content-because-the-table-has-no-usable-stamp.md), [0069](0069-clinical-care-audits-writes-to-its-own-append-only-table.md), [0064](0064-clinical-care-shares-the-legacy-database-and-coexists-with-legacy-encounter-writers.md)
- **Research:** `docs/clinical-care/16-soap-notes-research.md`

## Context

Every other record Clinical Care exposes has a uuid as its public identity (DOM-6). `form_soap` has none, and neither has the `forms` row that registers it. Legacy's own API addresses a note by its numeric id. The table also has no stamp, and legacy's REST update checks neither the
encounter nor the patient, so a numeric id from one patient can overwrite another's note there.

## Decision

1. **A SOAP note is addressed by its legacy form id, within its encounter:** `/encounters/{encounterUuid}/soap-notes/{noteId}`. We do not invent a uuid: that would need a new owned mapping table and a provisioning backfill, and every note legacy created would be
   unaddressable until it ran. The id is a database key and carries no secret; **the route's encounter and that encounter's patient are what authorise it**.
2. **Every statement matches the note through its `forms` row** (`formdir = 'soap'`, the encounter number, the patient, `deleted = 0`), exactly as vitals do. A note id that belongs to another encounter or patient is a 404, even if it exists. This is the check legacy's update lacks.
3. **The version is a content hash of the four sections** (ADR 0072), verified under a row lock, because the table has no stamp.
4. **A correction replaces the four sections and nothing else.** Legacy's update also rewrites `date` and `pid`; this service does neither (a correction is not a new note, and a note cannot move).
5. **The acting provider's username and group are recorded** as for vitals (legacy's REST path records neither).
6. Everything else follows the established pattern: one transaction for the note, its `forms` row, the outbox row and the audit row; refusals audited; column-level grants; field names, never text, in events and audit rows.

## Consequences

- The API has one resource identified by a number, an exception to DOM-6 that is contained to this resource and stated here. If legacy later adds a uuid to `form_soap`, the route can accept it without breaking this one.
- The id's meaning is only as stable as legacy's key. It is never reused (auto-increment), and a deleted note is soft-deleted, so a stale id is a 404, not another patient's note.
