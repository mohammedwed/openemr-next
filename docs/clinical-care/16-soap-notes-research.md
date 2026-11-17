# Clinical Care M5d slice 3: research for SOAP notes

- **Date:** 2026-11-17. **Decision:** [ADR 0073](../architecture/0073-soap-notes-are-addressed-by-their-form-id-within-their-encounter.md). Reuses ADR 0072 (content version), 0069 (audit) and the vitals registry pattern (`forms` row plus a form table).
- **Method:** read from the running OpenEMR 8.0.0 container and its database. `form_soap` is empty in this stack.
- **Scope decision:** this slice is **SOAP notes**. Clinical notes (`form_clinical_notes`) are a different shape (several notes grouped under one parent form, types and categories from lists, linked documents and procedure results, soft delete by `activity`) and become their own slice.

## 1. What legacy has

`form_soap`: `id` (auto-increment), `date` (datetime), `pid`, `user`, `groupname`, `authorized`, `activity`, and four `text` columns: `subjective`, `objective`, `assessment`, `plan`. **No `uuid`. No stamp** (no `last_updated`, no `ON UPDATE`).
Like vitals, a note belongs to an encounter only through its `forms` registry row (`formdir = 'soap'`, `form_id` = the note's `id`, `encounter` = the encounter **number**, `pid`, `deleted`).

## 2. Who writes it (LEG-8)

| Writer | How | Notes |
|---|---|---|
| The SOAP form (`C_FormSOAP`) | `FormSOAP->persist()` then `addForm(encounter, 'SOAP', id, 'soap', pid, userauthorized)` | The screen's own ORM; the date is set when the object is created |
| REST `POST /api/patient/{pid}/encounter/{eid}/soap_note` | `INSERT INTO form_soap SET date=NOW(), activity=1, pid, subjective..plan`, then `INSERT INTO forms SET date=NOW(), encounter, form_name='SOAP', authorized='1', form_id, pid, formdir='soap'` | Sets **neither `user` nor `groupname`** on either row |
| REST `PUT .../soap_note/{sid}` | `UPDATE form_soap SET date=NOW(), activity=1, pid=?, subjective..plan WHERE id=?` | **Checks neither the encounter nor the patient** (the vitals update has an IDOR guard, SOAP's has none), **overwrites `date` with now** and can **change `pid`**. Validation: each section optional, but 2 to 65535 characters if present |
| CCDA import | Bulk insert | |

## 3. Findings that shape the design

1. **No uuid and no stamp.** Legacy's own API addresses a note by its numeric id (`sid`). A uuid would need a new owned mapping table plus a provisioning backfill for notes legacy creates, and a note legacy created would be unaddressable until the backfill ran.
   ADR 0073 addresses a note by its form id **within its encounter**, which is safe because every statement also matches the encounter and patient through the `forms` row (the check legacy's update lacks).
2. **No stamp, so the version is a content hash** (ADR 0072), over the four sections. The screen and REST both rewrite the row without any stamp, so a content hash is the only thing that detects them.
3. **Legacy's update rewrites `date` and `pid`.** This service does neither: `date` stays when the note was written (a correction is not a new note), and a note cannot move to another patient or encounter.
4. **`text` columns hold 65,535 bytes, not characters** (the lesson of R-1 to R-7), so the limit is by UTF-8 size.
5. **REST sets no `user`.** This service records the acting provider's username and group, as it does for vitals, so the legacy screens show who wrote the note.
6. **No delete.** The `forms.deleted` flag is the legacy way; it needs the permission port.

## 4. Proposed rules (all new except where stated)

| Id | Rule |
|---|---|
| SN-1 | At least one of the four sections has text; an entirely empty note is refused. |
| SN-2 | A section that is given is at least 2 characters once trimmed (legacy's lower bound) and at most 65,535 **bytes**. Absent or blank means not written. |
| SN-3 | The note belongs to an existing encounter of a registered patient; a note can be reached only through its own encounter and patient. |
| SN-4 | A correction replaces the whole set of sections, conditional on the content version (`If-Match`, 428 and 412 as elsewhere). It does not touch `date`, `pid`, `user`, `groupname`, `authorized`, `activity` or the `forms` row. |
| SN-5 | One transaction: the note, its `forms` row, the outbox row and the audit row. A failing audit insert rolls everything back. |
| SN-6 | Events and audit rows name the **sections** that changed, never their text. |

## 5. Slices of the work

Record, get, list (an encounter's notes) and correct. No delete. Clinical notes follow as their own slice.
