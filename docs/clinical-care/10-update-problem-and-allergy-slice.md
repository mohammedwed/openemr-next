# Clinical Care M5c slice 3: update a problem or an allergy

- **Milestone:** ROADMAP M5c, slice 3. **Decisions:** [ADR 0068](../architecture/0068-clinical-care-updates-are-conditional-on-last-update-and-touch-only-owned-columns.md) (the concurrency model, applied here to `lists.modifydate`), [ADR 0070](../architecture/0070-clinical-care-owns-the-problem-and-allergy-issue-types-in-the-shared-lists-table.md) (ownership), [ADR 0069](../architecture/0069-clinical-care-audits-writes-to-its-own-append-only-table.md) (audit). **Builds on** [06](06-update-encounter-slice.md), the encounter update, whose behaviour this reproduces; [08](08-record-problem-slice.md) and [09](09-record-allergy-slice.md). This document records only what is specific to issues.
- **Endpoints:** `PUT /api/v1/problems/{uuid}`, `PUT /api/v1/allergies/{uuid}`, each with `If-Match`. **Status:** built; suites green (section 6).

## 1. What it does

Replaces a problem's or an allergy's **editable set** with what the caller sends, if and only if the record is still the version the caller read, and returns it as stored with
its new `ETag`. Status codes, headers and the order of checks are exactly the encounter's: 200, 400, 401, 404, 412 (with the current `ETag`), 428.

**Resolving a problem is an update, not a separate operation.** The issue screen marks a problem resolved by its `enddate`, not by `activity` (research doc, section 4), so
resolving sends an end date (and usually `outcome` 1), and **reopening sends none**. An allergy is ended and reopened the same way. There is therefore no delete in this
slice, and none is needed to take something off the active list.

## 2. What is editable

| Problem | Allergy |
|---|---|
| `title`, `beginDate` (required), `endDate`, `diagnosis`, `outcome`, `occurrence`, `referredBy`, `comments`, `verification` | `title`, `beginDate`, `endDate`, `diagnosis`, `reaction`, `severity`, `verification`, `comments` |

Not editable: `uuid`, the patient, the type, when and by whom it was recorded, `activity`, and every column another issue type owns. They are not in the change types, so
they cannot be in the statement. Nothing defaults: a `PUT` that silently filled a field in would rewrite something the caller never mentioned.

## 3. Rules (continuing PB-n and AL-n)

| Id | Rule | Where |
|---|---|---|
| IU-1 | The update replaces the whole editable set; a problem needs a title and a begin date, an allergy a title. Every other field may be cleared. | `ProblemChange`, `AllergyChange` |
| IU-2 | The update applies only if the row's `modifydate` still equals the version the caller read; otherwise nothing is written. | `EfProblemUpdater`, `EfAllergyUpdater` |
| IU-3 | A write without `If-Match` (or with `*`, or blank) is refused with 428. An unreadable or weak tag cannot match: 412. | `Preconditions` (shared with encounters) |
| IU-4 | If nothing differs from what is stored, nothing is written, no event is emitted and no audit row is written. Blank and `NULL` text are the same value; dates compare at whole-second precision. | `ProblemChangeSet`, `AllergyChangeSet` |
| IU-5 | Only the owned columns are in the statement; the database user holds `UPDATE` on exactly those eleven columns of `lists` and nothing else. | the updaters, seed script |
| IU-6 | **Every statement carries the type** (`medical_problem` or `allergy`). A problem `PUT` can never reach an allergy, a medication or a surgery, even by its exact uuid: it is a 404. | the updaters (ADR 0070 #2) |
| IU-7 | The field rules are the ones that record the issue: one implementation per aggregate (`ProblemFieldRules`, `AllergyFieldRules`) serves both the record and the change. | the domain |
| IU-8 | One transaction holds the conditional `UPDATE`, the `ProblemUpdated` or `AllergyUpdated` outbox row and the audit row; if the audit insert fails the update does not commit. | the updaters |
| IU-9 | The event and the audit row name the **fields** that changed, never their values. | use cases |
| IU-10 | The audit actor is **unknown** (`verified = 0`). An update names no acting user, and `lists` records only who created a row, so putting a provider id here would put a wrong name in the trail. | use cases (ADR 0069 #5) |
| IU-11 | `reaction` and `verification` are NOT NULL columns: clearing one writes the empty string, which reads back as none. `severity_al` is nullable. | the allergy updater |
| IU-12 | The order is: a malformed body is 400 at the binding boundary; then 428; then 404; then 412; then domain-rule 400s. | endpoints and use cases |
| PB-21 | **A problem's `verification`** is a code of the configured `condition-verification` list (**not** an allergy's list), compared exactly and no longer than `varchar(36)`; blank means none. It is part of recording a problem as well as updating one. | `ProblemFieldRules` |

## 4. What was shared

`UpdateStatus` and `UpdateOutcome` (updated, conflict, not found) were encounter-specific types and are now generic: three aggregates use them. `ConcurrencyToken`,
`Preconditions`, `RequestOrigins` and the audit machinery are the encounter slice's, unchanged. The field rules moved into one class per aggregate that both the record and the
change call, as `EncounterFieldRules` does for encounters, and the exact-match string-code check (`IssueFieldRules.StringCode`) is shared by problems and allergies. The refactors
changed no behaviour: the existing unit tests passed unchanged before the update code was added.

## 5. Known limitations

| # | Limitation | Why accepted |
|---|---|---|
| L1 | The `ETag` has whole-second precision; a lost commit acknowledgement is reported as 412 on retry, never a second write. | As for encounters (06, L1 and L2) |
| L2 | **The editor is not identified** (IU-10). | No verified identity until Phase 1b |
| L3 | **Protects our writes, not legacy's.** A later issue-screen save still overwrites ours silently, and the screen posts columns this service does not own. | Inherent to coexistence (ADR 0068) |
| L4 | No delete or inactivate. Taking an issue off the active list is done by ending it. | Delete waits for the permission port; legacy's delete needs `admin/super` |
| L5 | The substance and diagnosis are free text; `list_option_id` (the link to the `allergy_issue_list` code list) is not written. | A terminology decision (research Q7) |
| L6 | Allergy `outcome` and problem `classification` are not carried. | Not verified to be used (09 L4; research section 5) |

## 6. Tests

| Layer | What it pins |
|---|---|
| Unit (domain) | `ProblemChange` and `AllergyChange`: every shared rule, nothing defaults, whole-second dates, all violations together; problem `verification` (PB-21): exact match, the allergy list is not the problem list, an over-long configured code is a clean message |
| Unit (application) | `ProblemChangeSet` and `AllergyChangeSet` (one field at a time, null versus empty, resolving reports exactly the end date and outcome); both use cases against fakes: order of checks, no-op, conflict at write time, vanished row, event and audit content, refusals audited |
| Integration (adapters) | Every owned column changes; unowned columns survive; a legacy edit between read and write is detected; **an update of one type cannot reach another type's row by its uuid**; the NOT NULL columns; identical values still match; a failing audit rolls the update back; the column-level `UPDATE` grant (allowed on exactly the eleven columns, denied on identity, date, user, activity and other types' columns) |
| Integration (HTTP) | 200, 400, 401, 404, 412, 428 and the `ETag` round trip for both endpoints; resolving then reopening; the order of checks; no-op writes nothing; audit and event rows; another type's uuid is a 404 |
