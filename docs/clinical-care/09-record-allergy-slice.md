# Clinical Care M5c slice 2: record, get and list allergies

- **Milestone:** ROADMAP M5c, slice 2. **Decisions:** [ADR 0070](../architecture/0070-clinical-care-owns-the-problem-and-allergy-issue-types-in-the-shared-lists-table.md), [ADR 0068](../architecture/0068-clinical-care-updates-are-conditional-on-last-update-and-touch-only-owned-columns.md) (the `ETag`), [ADR 0069](../architecture/0069-clinical-care-audits-writes-to-its-own-append-only-table.md) (audit). **Research:** [07](07-problems-and-allergies-research.md). **Same shape as** [08](08-record-problem-slice.md); this document records only what differs.
- **Endpoints:** `POST /api/v1/allergies`, `GET /api/v1/allergies/{uuid}`, `GET /api/v1/allergies?patientUuid=`. **Status:** built; suites green (section 6).

## 1. What it does

Records an allergy on a registered patient's list, reads one back with an `ETag`, and lists a patient's allergies newest first, one bounded page at a time.
An allergy is a row of legacy's `lists` table with `type = 'allergy'`. Status codes are as for problems (201, 400, 401, 404; 200 with an `ETag` for a get).
**A problem's uuid is a 404 here, and an allergy's uuid is a 404 under `/problems`** (ADR 0070 #2): the two aggregates share a table and nothing else.

## 2. What differs from a problem

| | Problem | Allergy |
|---|---|---|
| Begin date | **Required** (`ConditionValidator`) | **Optional** (`AllergyIntoleranceValidator`) |
| Coded fields | `outcome`, `occurrence`: **integers** | `reaction`, `severity_al`, `verification`: **strings**, compared exactly |
| Code lists | `outcome`, `occurrence` | `reaction`, `severity_ccda`, `allergyintolerance-verification` |
| Blank code | `0` is always allowed | Blank means none; any given code must be configured |
| NOT NULL columns | none involved | `reaction` and `verification` are `NOT NULL DEFAULT ''`: an absent one is written `''`, and `''` reads back as none |
| `lists_touch` type | `medical_problem` | `allergy` |
| Referred by, outcome, occurrence | owned | not owned |

## 3. Rules

| Id | Rule | Legacy source | Parity | Where |
|---|---|---|---|---|
| AL-1 | A title (the substance) is required, 2 to 255 characters after trimming. | `AllergyIntoleranceValidator` | Parity; trimming is **(new)** | `IssueFieldRules.Title` |
| AL-2 | Begin and end dates are optional; if given, in 1900-01-01 to a year from now, and the end not before the begin. | validator: `optional` dates; the range is CC-17 | Optional: parity. Range and order: **(new)** | `IssueFieldRules.Dates` |
| AL-3 | A diagnosis is optional; if given, 2 to 255 characters of `SYSTEM:code` pairs. | validator: `lengthBetween(2, 255)` | Length: parity. Shape: **(new)** | `IssueFieldRules.Diagnosis` |
| AL-4 | `reaction`, if given, is a code of the configured `reaction` list, compared exactly, and fits `varchar(255)`. | the issue screen picks from the list; REST accepts anything | Stricter than REST, equal to the screen: **(new)** | `AllergyRecording.Create` |
| AL-5 | `severity_al`, if given, is a code of the configured `severity_ccda` list, and fits `varchar(50)`. | as AL-4 | as AL-4 | as AL-4 |
| AL-6 | `verification`, if given, is a code of the configured `allergyintolerance-verification` list (**not** a problem's `condition-verification`), and fits `varchar(36)`. | the screen picks the list by issue type | Parity with the screen | as AL-4 |
| AL-7 | `comments` are at most 65,535 characters. | a `longtext` | **(new)** input bound (SEC-7) | `IssueFieldRules.Comments` |
| AL-8 | The acting provider is a positive id within the column's range. | the session's user | Parity in content; caller-supplied until F-CC-1 | `IssueFieldRules.Provider` |
| AL-9 | A configured option id **longer than the column that stores it** is a clean 400, never a database error. | a site can configure `option_id` up to 100 characters | **(new)**, review findings R-1 to R-7 | `AllergyRecording.Create` |
| AL-10 | `lists.date` is `NOW()`; the row carries `type = 'allergy'`, `activity = 1`, the provider's username and group; the uuid is registered; `lists_touch (pid, 'allergy')` is inserted if absent. | REST path; the issue screen's `setListTouch` | As PB-10 to PB-13 (ADR 0070 #5) | `EfAllergyRegistry`, `IssueRows` |
| AL-11 | One transaction holds the row, `uuid_registry`, `lists_touch`, the `AllergyRecorded` event (ids only) and the audit row. | none | **(new)**, EVT-1, EVT-5, SEC-9 | `EfAllergyRegistry` |
| AL-12 | **Every statement filters on `type = 'allergy'`.** | the issue screen is generic over types | **(new)**, ADR 0070 #2 | `EfAllergyRegistry`, `EfAllergyReader` |
| AL-13 | A row with no `uuid` is not addressable and is left out of lists and totals. | the issue screen assigns none | Tolerant read, ADR 0070 #6 | `EfAllergyReader` |
| AL-14 | A refused request leaves a `failed` audit row naming the patient asked about. | none | **(new)**, ADR 0069 #4 | `RecordAllergyUseCase` |

## 4. What was shared, and what was not

Title, date, diagnosis, comment and provider rules moved into one internal class, `IssueFieldRules`, used by both aggregates, so a problem and an allergy cannot
disagree about what a valid title or date range is (the same move as `EncounterFieldRules`). The `uuid_registry` and `lists_touch` writes moved into
`IssueRows`. Each aggregate keeps its own `INSERT INTO lists`, its own reader and its own endpoints, because the columns, the code lists and the required
fields differ. The refactor changed no problem behaviour: all 362 existing unit tests passed unchanged.

## 5. Known limitations

| # | Limitation | Why accepted |
|---|---|---|
| L1 | A uuid-less allergy created in the issue screen is invisible until legacy gives it a uuid (AL-13). | As problems (ADR 0070 #6) |
| L2 | The actor is the provider the caller named, flagged unverified. | No verified identity until Phase 1b |
| L3 | No update, resolve or delete yet. | Slice 3 (resolving is an update) and the permission port |
| L4 | `outcome` is **not** an allergy field here (the research listed it). | Not verified to be used for allergies in the 8.0.0 screen; a narrower owned set is the safe direction |
| L5 | The substance is free text: `list_option_id`, which links the title to the `allergy_issue_list` code list, is not written. | A terminology decision (Q7); a later slice can add it |
| L6 | **Problems have a `verification` field too** (list `condition-verification`) that slice 1 did not carry. | Found while researching this slice; it comes with problem update (slice 3) |

## 6. Tests

| Layer | What it pins |
|---|---|
| Unit | `AllergyRecording` (AL-1 to AL-9, including the over-long configured code and exact-match codes), `Allergy`, the use cases against fakes (event and audit content, refusals audited, nothing allocated on refusal, the empty patient uuid) |
| Integration (adapters) | The legacy-shaped row, the NOT NULL columns, an absent begin date, uuid and `lists_touch` (the two lists touched separately), retry-once, no partial write, **no commit without the audit row**, other types untouched; the reader against other types, other patients, uuid-less, half-filled, orphan rows and legacy's empty strings, ordering and paging; the three code lists, which never leak into each other |
| Integration (HTTP) | 201, 400 (every violation together, a problem's verification value rejected, an over-long code), 401, 404, the `ETag` and its change under another writer, **a problem's uuid is a 404 as an allergy and the reverse**, lists exclude problems, other patients and uuid-less rows |
| Grants | `SELECT` and `INSERT` now include `reaction`, `severity_al` and `verification`; `list_option_id` and `subtype` stay uninsertable |
