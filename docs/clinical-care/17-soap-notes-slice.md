# Clinical Care M5d slice 3: record, read, list and correct SOAP notes

- **Milestone:** ROADMAP M5d, slice 3. **Research:** [16](16-soap-notes-research.md). **Decisions:** [ADR 0073](../architecture/0073-soap-notes-are-addressed-by-their-form-id-within-their-encounter.md) (addressed by form id within the encounter), [ADR 0072](../architecture/0072-immunizations-are-versioned-by-content-because-the-table-has-no-usable-stamp.md) (versioned by content), ADR 0069 (audit).
- **Endpoints:** `POST /api/v1/encounters/{encounterUuid}/soap-notes`, `GET .../soap-notes/{noteId}`, `GET .../soap-notes` (paged), `PUT .../soap-notes/{noteId}` with `If-Match`. **Status:** built; suites green (section 5).

## 1. What it does

Records a SOAP note (subjective, objective, assessment, plan) on an encounter, reads and lists an encounter's notes, and corrects one by replacing its sections, if and only if it is still the version the caller read. The contract is the other updates': 200 with the new `ETag`,
400, 401, 404, 412 carrying the current `ETag`, 428, in the same order of checks. The `ETag` is a content version (`"c-<32 hex>"`) because `form_soap` has no stamp.

## 2. Rules

| Id | Rule | Legacy source | Parity | Where |
|---|---|---|---|---|
| SN-1 | At least one of the four sections has text. | none (legacy accepts an empty note) | **(new)** | `SoapSections` |
| SN-2 | A section that is given has at least 2 characters once trimmed (legacy's lower bound) and at most 65,535 **bytes** (the column is `text`). Blank means not written. Text is kept exactly as written. | REST validator: 2 to 65,535 | Parity, with bytes instead of characters | `SoapSections` |
| SN-3 | A note is reached **only through its own encounter and patient**, matched through its `forms` row (`formdir = 'soap'`, encounter number, pid, `deleted = 0`) on every statement. A note id of another encounter or patient is a 404 even though it exists. | legacy's update checks neither | **(new)**, stricter | reader, updater |
| SN-4 | A correction replaces the whole set of sections, conditional on the content version. It writes the four section columns and nothing else: **not `date` and not `pid`** (legacy's update rewrites both), not who wrote it, not `authorized` or `activity`, not the `forms` row. | legacy rewrites date and pid | **(new)**, deliberately different | updater |
| SN-5 | One transaction holds the note, its `forms` row, the outbox row and the audit row; a failing audit insert rolls everything back. A correction does the same for its row, event and audit row. | none | **(new)**, EVT-1, SEC-9 | registry, updater |
| SN-6 | Events and audit rows name the **sections** written or changed, never their text. A note is named in the audit trail as `soap:{id}` (it has no uuid; the key fits the column that holds a uuid's text). | none | **(new)**, EVT-5, SEC-9 | use cases, `AuditEntry` |
| SN-7 | The acting provider's username and group are recorded on both rows, as for vitals (legacy's REST path leaves them empty). | the session's user | Parity with the screen | registry |
| SN-8 | The version is a hash of the four sections, verified under a row lock (ADR 0072): a legacy edit, which moves no stamp, is detected, even one landing between the caller's read and the write. | none | **(new)**, LEG-10 | updater, `SoapNoteView.Version` |
| SN-9 | Nothing changed means nothing written, no event, no audit row. | none | **(new)** | use case |
| SN-10 | The audit actor is the named provider on a record (unverified) and **unknown** on a correction. | the session's user | Deliberate (ADR 0069 #5) | use cases |

## 3. Known limitations

| # | Limitation | Why accepted |
|---|---|---|
| L1 | A note's public identity is a number, an exception to DOM-6 contained to this resource (ADR 0073). | `form_soap` has no uuid; the encounter in the route authorises it |
| L2 | ~~**No retry guard.**~~ **Resolved (M5h, ADR 0077):** the registry gives each attempt an id, writes it into the event in the same transaction, and finds it on a re-run. A test loses a real commit acknowledgement and shows one note. | A client retry is covered by the `Idempotency-Key` |
| L3 | Legacy's `REST` and screen writers rewrite a note with no stamp, so one that runs after this service wins silently. | Inherent to coexistence (ADR 0072) |
| L4 | No delete (`forms.deleted` needs the permission port). | As elsewhere |
| L5 | The audit table's `subject` column holds either a uuid's text or a `soap:{id}` key. A reader of that column must read it as text (the connector parses `CHAR(36)` as a Guid). Production only inserts. | One column, two kinds of subject, documented here and in `AuditEntry` |
| L6 | Clinical notes (`form_clinical_notes`) are not covered; they are a different shape. | Next slice |

## 4. Contract

`SoapNoteResponse` carries an `eTag` (also on every list item); `If-Match` is declared on the PUT with 412 and 428 documented. OpenAPI snapshot regenerated. The grants are `SELECT, INSERT` on `form_soap` and `UPDATE` on exactly the four section columns; `forms` is already granted.

## 5. Tests

| Layer | What it pins |
|---|---|
| Unit (domain) | Section rules and exact messages (empty note, two characters, bytes not characters, every violation together); formatting preserved; the aggregate and provider bound; **every property of the view is a section or deliberately outside the version** |
| Unit (application) | Record, correct, get, list against fakes: order of checks, no-op writes nothing, version carried, refusals audited under `soap:{id}`, a half-filled encounter has no notes; the keyed audit subject (`AuditEntry`) |
| Integration (adapter) | Round trip and recorded-by; the `forms` row; event and audit; failing audit rolls back; **another encounter's or patient's note is not found**, soft-deleted and duplicated `forms` rows; tolerant reads; paging; a correction leaves date, pid, who and the `forms` row untouched; legacy edit detected; **six concurrent corrections, one winner** |
| Integration (HTTP) | 201, 200, 400, 401, 404, 412, 428; the `ETag` round trip; a stamp-shaped tag refused; clearing a section; the order of checks; no-op; audit and event content |
| Integration (grants) | Update allowed on exactly the four sections; denied on date, pid, user, group, authorized, activity; no delete |
