# Clinical Care M5c slice 4: link problems and allergies to an encounter

- **Milestone:** ROADMAP M5c, slice 4. **Decisions:** [ADR 0070](../architecture/0070-clinical-care-owns-the-problem-and-allergy-issue-types-in-the-shared-lists-table.md) (the issue types this service owns), [ADR 0069](../architecture/0069-clinical-care-audits-writes-to-its-own-append-only-table.md) (audit). **Builds on** [01](01-create-encounter-slice.md) (the encounter), [08](08-record-problem-slice.md) and [09](09-record-allergy-slice.md). This is the "link issues to the encounter" item slice 1 deferred until a problem list context existed.
- **Endpoints:** `PUT /api/v1/encounters/{encounterUuid}/issues/{issueUuid}`, `DELETE` on the same path, `GET /api/v1/encounters/{encounterUuid}/issues`. **Status:** built; suites green (section 6).

## 1. What it does

Records which problems and allergies an encounter addressed: legacy's `issue_encounter` table. The routes are under the encounter, which the gateway already sends here, and name the issue
by its uuid: the link is a relation between two resources that already exist, not a resource of its own.

| Call | Outcomes |
|---|---|
| `PUT .../issues/{issueUuid}` (optional body `{ "resolved": bool }`) | **201** a new link; **200** the link was already there (the stored one is returned); **400** (the issue is another patient's, or the encounter has no number or no known patient); **401**; **404** (no such encounter, or no such problem or allergy, **or the uuid is a medication, surgery or any other type**) |
| `DELETE .../issues/{issueUuid}` | **204** whether or not a link existed; **404** (no such encounter or issue); **401** |
| `GET .../issues?limit=&offset=` | **200** (kind, title, resolved, link uuid, per issue; paged, total); **400** (page out of bounds); **404** (no such encounter) |

## 2. Rules

| Id | Rule | Legacy source | Parity | Where |
|---|---|---|---|---|
| IE-1 | An encounter must have a number to be linked to: the link row stores the encounter **number**, not its uuid. | `issue_encounter.encounter` | Parity | `EncounterIssueLink.Create` |
| IE-2 | The encounter's patient must be known. | `issue_encounter.pid` | Parity | `EncounterIssueLink.Create` |
| **IE-3** | **The issue and the encounter must be the same patient's.** | `linkIssueToEncounter` takes a patient id and trusts it | **Stricter than legacy, deliberately**: a clinical record naming another patient's problem is a safety defect | `EncounterIssueLink.Create` |
| IE-4 | Linking is idempotent: an existing link for the same patient, issue and encounter is returned as it is, and **nothing is written, emitted or audited** for it. | `linkIssueToEncounter` ("already connected") | Parity | `EfEncounterIssueRegistry` |
| IE-5 | A link is **never updated**, so the `resolved` flag of an existing link is not changed by a second `PUT`. To change it, unlink and link again. | legacy has no link update either | Parity | `LinkIssueToEncounterUseCase` |
| IE-6 | Only problems and allergies can be linked. Any other type, including one a site added, is a 404, even by its exact uuid; an issue with no uuid is not addressable. | the encounter screen links any type | **(new)**, ADR 0070 #1 and #2 | `EfIssueDirectory` |
| IE-7 | The new link row has `created_by` and `updated_by` **null**: the actor is unknown (ADR 0069 #5) and a guessed user id would be written into a clinical table. | the session's user id | **Deliberate deviation**; fixed when identity forwarding exists | `EfEncounterIssueRegistry` |
| IE-8 | One transaction holds the link row, its `uuid_registry` row, the `IssueLinkedToEncounter` event and the audit row. If the audit insert fails, the link does not commit. | none (legacy has no transaction) | **(new)**, EVT-1, SEC-9 | `EfEncounterIssueRegistry` |
| IE-9 | An unlink deletes the **link row only**, never the issue or the encounter, and writes its event and audit row in the same transaction. A missing link is not an error and writes nothing. | `unlinkIssueFromEncounter` | Parity, idempotent | `EfEncounterIssueRegistry` |
| IE-10 | An unlink **does not delete the link's `uuid_registry` row**. | legacy deletes it | **Deliberate deviation**: it would need `DELETE` on a table shared by every uuid in the system. A stale registry row is harmless, because a uuid is never reused | `EfEncounterIssueRegistry` |
| IE-11 | The event and the audit row carry ids and the issue's kind (`problem` or `allergy`) only: no title, no resolved flag. | none | **(new)**, EVT-5, SEC-9 | use cases |
| IE-12 | The list shows only problems and allergies, and joins the issue on its row id **and** its patient. A link legacy made between two patients' records, and a link whose issue legacy's REST `DELETE` removed (it leaves the link behind), are not shown. | legacy shows whatever is joined | Tolerant read (DOM-9), stricter than legacy | `EfEncounterIssueReader` |
| IE-13 | A refused link or unlink leaves a `failed` audit row naming the encounter, and writes nothing else. | none | **(new)**, ADR 0069 #4 | the use cases |

## 3. Why an unlink is not "delete" in the sense this project avoids

Delete of clinical records waits for the permission port, because legacy needs `admin/super` for it and the records matter. An unlink removes a **relation** that carries no clinical text, only
ids and a flag, and legacy's own encounter screen rewrites the set of links on every save for any user who can edit the encounter. It is therefore built now, with parity of effect and a
stricter audit than legacy has: every removal leaves an audit row and an event, and the issue and the encounter stay exactly as they were.

## 4. Grants

`SELECT, INSERT, DELETE` on `issue_encounter` (a table of ids, a resolved flag and two actor columns), and no `UPDATE`: links are never edited (IE-5). The existing `SELECT` on the `lists`
columns serves the join, and `INSERT` on `uuid_registry` serves the link's own uuid. The service still has no `DELETE` on `uuid_registry`.

## 5. Known limitations

| # | Limitation | Why accepted |
|---|---|---|
| L1 | The actor columns of a new link are null, and the audit actor is unknown (IE-7). | No verified identity until Phase 1b |
| L2 | A retry after a lost commit acknowledgement reports the link as already there (200 instead of 201). It never writes a second one. | The table's unique key makes the second attempt find the first |
| L3 | Links to medications, surgeries and other types are invisible and cannot be made (IE-6). | Those types have no owner yet (ADR 0070) |
| L4 | There is no "list the encounters an issue was addressed in". | A read need, not yet asked for; the data is the same table |
| L5 | A link created by the legacy encounter screen for an issue with no uuid is not shown (IE-12). | As ADR 0070 #6 |

## 6. Tests

| Layer | What it pins |
|---|---|
| Unit (domain) | `EncounterIssueLink`: the same-patient rule, no number, unknown patient, all reported together, the id assigned once |
| Unit (application) | The three use cases against fakes: what reaches the registry, an existing link reported as found with the stored flag, no clinical content in the event or the audit row, nothing allocated on refusal, bounds before lookup, every refusal audited |
| Integration (adapters) | The legacy-shaped row with null actor columns; idempotence on the unique key and a link legacy made first left untouched; atomicity, **no commit without the audit row** for a link and for an unlink; unlink removes only that encounter's link and leaves the issue and the registry row; the reader against other types, a cross-patient link, an orphaned link, an issue with no uuid, paging; the issue directory finds a problem and an allergy and nothing else; the grants (`DELETE` allowed on the link table, `UPDATE` and `uuid_registry` deletes denied) |
| Integration (HTTP) | 201 then 200, the stored flag wins, no second event or audit row, 404 for an unknown encounter or issue and for a medication or surgery, **400 for another patient's issue**, 204 twice, the issue and encounter survive an unlink, the list, 401 on every route |
