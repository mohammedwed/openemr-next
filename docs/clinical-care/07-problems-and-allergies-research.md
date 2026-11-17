# Clinical Care M5c: research for the problem list and allergies

- **Milestone:** ROADMAP M5c (problem list and allergies). **Phase 0: research and decisions, no code.**
- **Date:** 2026-11-17. **Decisions:** [ADR 0070](../architecture/0070-clinical-care-owns-the-problem-and-allergy-issue-types-in-the-shared-lists-table.md). Reuses [ADR 0068](../architecture/0068-clinical-care-updates-are-conditional-on-last-update-and-touch-only-owned-columns.md) (concurrency) and [ADR 0069](../architecture/0069-clinical-care-audits-writes-to-its-own-append-only-table.md) (audit).
- **Method:** legacy behaviour read from the **running OpenEMR 8.0.0 container**, not the 8.6.0-dev tree; columns read from the live database. The container's `grep` is BusyBox (no `--include`).

## 1. What legacy calls it

Legacy has no "problem list" or "allergy" table. Both are rows of **`lists`**, one table for every kind of **issue**, told apart by a free-text `type`
column. The configured types live in `issue_types` (`active`, `category`, `type`, `aco_spec`, ...): `medical_problem`, `allergy`, `health_concern`,
`medication`, `surgery`, `dental`, `medical_device`, plus IPPF-specific ones. A site can add more, so the set is **data, not code**.

| Fact | Value |
|---|---|
| Table | `lists` (36 columns); children `lists_medication`, `issue_encounter`, `lists_touch` |
| Identity | `id` (bigint, auto), `uuid` (binary(16), **nullable**), `pid` |
| **Concurrency token** | `modifydate` `timestamp`, `ON UPDATE CURRENT_TIMESTAMP`, whole seconds: the same model as ADR 0068, no schema change |
| Soft state | `enddate` (resolved), `activity` (a flag, see 4), `outcome` (list `outcome`: 1 Resolved, 2 Improved, ...) |
| Code lists | `severity_ccda`, `reaction`, `occurrence`, `outcome`, `allergy_issue_verification`, `issue_subtypes` (all in `list_options`) |
| This stack | `lists` is empty: there is no data to shadow-compare yet, so fixtures are needed |

## 2. Who writes `lists` (LEG-8)

Ten files write it, by three routes that **do not behave the same**:

| Route | Writers | Notes |
|---|---|---|
| **The issue screen** | `add_edit_issue.php` through `PatientIssuesService::createIssue` / `updateIssue` | The path clinicians use. Generic over every type |
| **REST / FHIR** | `AllergyIntoleranceService`, `ConditionService` (problems), and the medication, surgery and dental REST controllers | `POST/PUT/DELETE /api/patient/:puuid/allergy` and `.../medical_problem`; `DELETE` is a **hard** `DELETE FROM lists` |
| **Imports and side doors** | `Carecoordination` and `Ccr` (CCDA/CCR import: 13 statements), `CdaTemplateImportDispose`, `eye_mag/save.php`, `fee_sheet_queries.php`, `deletedrug.php`, `ListService` | Bulk and form-specific writes of any column |
| **Delete** | `deleter.php` (issue branch) | Hard delete plus `issue_encounter` and `lists_medication`, **`admin/super` only** |

**Consequence:** as with encounters, "single writer" is false. Updates are conditional on `modifydate` and name only the columns we own (ADR 0068).

## 3. The two legacy paths disagree (so "parity" must pick one, per behaviour)

| Behaviour | Issue screen (UI) | REST (`AllergyIntoleranceService`) | Decision for us |
|---|---|---|---|
| `uuid` at insert | **Not assigned.** Backfilled later when any service is constructed (`createMissingUuidsForTables`) | Assigned and registered in `uuid_registry` | **Assign at insert** (REST). A UI-created row is uuid-less until legacy backfills it: see Q2 |
| `date` at insert | `date("Y-m-d H:m:s")`: **`m` is the month**, so the minutes hold the month number (a legacy bug) | `NOW()` | **`NOW()`** (REST). Deliberate deviation, recorded |
| `user`, `groupname` | From the session | Not set | Provider named by the caller, as in CC-10, until identity forwarding (Phase 1b) |
| `lists_touch` | `setListTouch(pid, type)` inserts a row if none | Not written | Parity with the UI (Q4) |
| `activity` at insert | `1` | `1` | `1` |
| Link to encounter | Optional `issue_encounter` row | none | Separate slice (Q6) |
| Delete | Hard (`deleter.php`, super admin) | Hard, **no cascade**: leaves `issue_encounter` rows orphaned | Not in this slice (Q5) |
| ACL | `AclMain::aclCheckIssue(type, '', write|addonly)` per type, plus the patient's squad | Route-level `patients/med` | A permission port, later (3b) |
| Validation | `issue_types` must contain the type; nothing else | `AllergyIntoleranceValidator` | The domain owns the rules (below) |

## 4. How "inactive" works

The UI list marks a problem **resolved** by `enddate` (a muted row), not by `activity`. `activity` is a vestigial flag legacy helpers flip
(`library/lists.inc.php`: `update lists set activity = '0'`), and `report.inc.php` filters on it. So **resolving a problem is an update** (set
`enddate`, usually `outcome = 1`), and "inactivating" is not a different operation. This slice needs no delete.

## 5. Columns: owned and not owned

| Type | Owned (editable) | Notes |
|---|---|---|
| **Problem** (`medical_problem`) | `title`, `begdate`, `enddate`, `diagnosis`, `outcome`, `occurrence`, `referredby`, `comments` | `occurrence` and `outcome` take the list codes; `diagnosis` is `SYSTEM:code;SYSTEM:code` text. `classification` is **not owned**: it is an injury classification for other issue types and has no code list in `list_options` |
| **Allergy** (`allergy`) | `title`, `begdate`, `enddate`, `diagnosis`, `reaction`, `severity_al`, `verification`, `outcome`, `comments` | `reaction`, `severity_al`, `verification` take the list codes |

**Never written by this service:** `id`, `uuid` (set once, at insert), `pid`, `type` (fixed per aggregate), `user`/`groupname` after insert, `modifydate`
(the database sets it), and every column a different type owns (`erx_*`, `external_*`, `udi*`, `injury_*`, `reinjury_id`, `destination`, `returndate`,
`extrainfo`, `list_option_id`, `subtype`). The adapter's statements always carry `AND type = '<its type>'`, so a problem endpoint can never touch a
surgery or a medication that happens to share a uuid or id.

## 6. Findings that shape the plan

1. **One table, many owners.** Medications, surgeries, dental issues and devices are rows in the same table with other future owners (Pharmacy,
   ...). Ownership is by `type`, and the adapter must filter on it in **every** statement, including the grant: a column-level grant cannot say "only
   rows of type allergy", so this is enforced in code and pinned by tests (ADR 0070).
2. **The design documents disagree.** The DDD blueprint lists "Patient Issue" in Patient Management's glossary and also Condition and Allergy in
   Clinical Care. ADR 0070 settles it: Clinical Care owns the `medical_problem` and `allergy` types; the generic "issue" is not a context of its own.
3. **uuid-less rows are normal.** A problem created in the UI has no uuid until something backfills it, so a reader must not assume one (CC-16
   precedent). Q2.
4. **Hard deletes by other writers leave orphans** in `issue_encounter` (REST) that this service must tolerate on read.
5. **No data to compare yet.** The shadow comparison (slice 1 section 12) needs fixtures, created by driving legacy's own `PatientIssuesService` and REST
   endpoints in the running container.
6. **Delete waits for the permission port,** like encounters (3b). Resolving needs no delete.

## 7. Questions and the answers (2026-11-17: all defaults accepted)

| # | Question | Answer |
|---|---|---|
| Q1 | Which issue types does Clinical Care own? | **`medical_problem` and `allergy` only.** `health_concern` and the rest stay with legacy until a context owns them |
| Q2 | How are rows with no `uuid` handled? | **Not addressable and left out of lists** (CC-16). No backfill write on read. Revisit if the shadow comparison shows UI rows that stay invisible for long |
| Q3 | `date` at insert: the UI's buggy `H:m:s` or `NOW()`? | **`NOW()`** |
| Q4 | Write `lists_touch` on create, as the UI does? | **Yes**, one `INSERT IGNORE` (`(pid, type)` is the primary key). Legacy's summary reads it as `listTouched` for the allergy, problem and medication cards: the list "has been addressed" |
| Q5 | Delete? | **None in this slice.** Resolve through update. Soft delete after the permission port |
| Q6 | Link an issue to an encounter (`issue_encounter`)? | **A separate later slice** (the slice 1 doc deferred it until this context existed) |
| Q7 | Validate `diagnosis` codes? | **Shape only** (`SYSTEM:code` pairs, at most 255 characters); no code-system lookup |
| Q8 | One adapter for both types? | **One shared adapter helper, two aggregates** (`Problem`, `Allergy`): separate rules and endpoints, one place that knows the `lists` layout (LEG-2) |

## 8. Proposed slices (M5c)

| Slice | Content |
|---|---|
| 1 | **Record a problem**, **get**, **list a patient's problems** (create, `ETag` on read, audit, outbox event, `lists_touch`) |
| 2 | **Record an allergy**, **get**, **list** (same pattern; adds reaction, severity, verification) |
| 3 | **Update** problem and allergy (`PUT`, `If-Match`; resolving is an update) |
| 4 | **Link an issue to an encounter** (`issue_encounter`) |
| 5 | WebApp: Problems and Allergies tabs on the patient page |
| 6 | Shadow comparison against legacy, a layered review, and the legacy-UI check (needs your sign-in) |

The concurrency, audit and `ETag` machinery from M5b is reused unchanged; what is new is the `type`-scoped adapter and the code lists
(`severity_ccda`, `reaction`, `verification`, `outcome`, `occurrence`).
