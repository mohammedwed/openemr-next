# Clinical Care M5c slice 6: shadow comparison against legacy, and the layered review

- **Date:** 2026-11-17. **Scope:** the problem list, the allergy list, issue update and the encounter links (slices 1 to 4), plus what slice 5 depends on. Method as for slice 1 (`01-create-encounter-slice.md` section 12) and `04-layered-review.md`.
- **Outcome in one line:** the shadow comparison compared **229 columns: 215 identical, 14 differ, every difference is a documented rule**. The review found **one defect, fixed and pinned (R-15: a link could be written against the wrong encounter)**, removed **dead code**, and added **a structural guard** for the milestone's central safety rule. **Still outstanding:** the legacy-UI check and a browser check of the new screens (both need a signed-in user), and the open gateway-authentication finding, which the new routes inherit.

## 1. Shadow comparison

Each scenario was run **twice on the live stack**: once by OpenEMR 8.0.0's own service, run inside its container, and once through the new API (via the gateway), for the same patient and inputs. Every column of the resulting rows was then compared except the ones allocated per row (`id`, `uuid`, `date`, `modifydate`). The runner and its cleanup are in the session's scratchpad, not the repo; every row it made was removed afterwards and the database was left as found (including one stale `uuid_registry` row, which is rule IE-10 observed).

| Scenario | Legacy side | Columns identical | Differ |
|---|---|---|---|
| A1 allergy, title only | `AllergyIntoleranceService::insert` | 30 | 2 (`user`, `groupname`) |
| A2 allergy, every owned field | the same | 30 | 2 (`user`, `groupname`) |
| P1 problem, title and begin date | `ConditionService::insert` | 30 | 2 (`user`, `groupname`) |
| P2 problem, every owned field | the same | 30 | 2 (`user`, `groupname`) |
| P3 problem, every owned field | **the issue screen's path**, `PatientIssuesService::createIssue` | 32 | **0** |
| UA allergy update | `AllergyIntoleranceService::update` | 30 | 2 (`user`, `groupname`, inherited from the create) |
| UP problem update | the issue screen's `updateIssue` | 30 | 2 (`user`, `groupname`, inherited from the create) |
| LK link | `PatientIssuesService::linkIssueToEncounter` | 3 | 2 (`created_by`, `updated_by`) |
| `uuid_registry` (a problem, a link) | | identical | 0 |
| `lists_touch` | | `allergy` and `medical_problem`, as the issue screen writes | 0 |

**Total: 215 identical, 14 differ**, and none is unexplained:

- **`user` and `groupname` (12 of the 14).** Legacy's REST path leaves them NULL; this service records the acting provider's username and group, as the issue screen does (rule PB-11). Against the screen's own path (P3) there is **no difference at all**. The updates differ only because the two rows already differed from their creates; the update statements themselves leave both columns alone on both sides.
- **`created_by` and `updated_by` on a link (2 of the 14).** Legacy writes the session's user id; this service writes NULL because it does not know who is acting (rule IE-7, ADR 0069 #5).

**Observed on the live system, confirming the research (`07-problems-and-allergies-research.md`):**

- A row made by the issue screen has **no uuid** (P3), while REST-made rows get one and a `uuid_registry` row at insert. A problem made in the screen is therefore invisible to this API until something backfills it (limitation L1 of slice 1, ADR 0070 #6). This is measured, not assumed.
- The screen's `date("Y-m-d H:m:s")` puts the **month** where the minutes belong: a row made at `10:22:17` was stamped `10:11:17`. This service stamps the real time (rule PB-10).
- Legacy's update and this service's update produce the **same stored values** for every owned column.

**What the comparison does not cover:** deletes (not built), medications and other types (not owned), a provider other than user 1, and the legacy *UI* itself (section 3). The comparison was run against the REST and service entry points, not the rendered screens.

## 2. Layered review

**How the defect was found.** The earlier review asked, of every column written, *can the domain accept a value this column cannot hold?* Applied to the new tables it flagged the link: `issue_encounter.encounter` and `list_id` are **signed** `int(11)`, but an encounter number comes from `sequences.id`, an **unsigned** int (up to 4,294,967,295), and is stored in `form_encounter.encounter`, a bigint. Probes were written first and **all four failed** against the code as committed.

| # | Finding | Status |
|---|---|---|
| **R-15** | **A link to a high-numbered encounter was written against a different one.** The link used `INSERT IGNORE`, which turns a data error into a warning and **clamps** the value: 3,000,000,000 became 2,147,483,647 (`R_15_this_is_why_the_link_does_not_use_insert_ignore...` pins that on the real database). Rare (it needs over two billion encounters) but it is silent corruption of a clinical link, in the dangerous direction. | **Fixed.** The domain refuses an encounter number or issue id above 2,147,483,647 (a 400 with a message), and the insert is now `INSERT ... SELECT ... WHERE NOT EXISTS`, which is idempotent without masking any other error. Pinned at four levels: domain, HTTP (a real encounter numbered 3,000,000,000), the database's own behaviour, and a plain `INSERT` being refused |
| R-16 | The central safety rule (ADR 0070 #2, every statement on `lists` names `type`) was enforced only by behavioural tests. | **Guard added:** `IssueTypeScopeTests` reads the SQL and fails the build if any statement on `lists` omits `type`. **Mutation-tested**: removing one filter failed it; restoring it passed |
| R-17 | Three audit-action constants (`encounter.delete`, `vitals.correct`, `vitals.delete`) were defined early for phases not built, and nothing used them. | **Removed.** Every remaining constant is used |
| R-18 | A race between two callers making the very same link can collide on the unique key (one fails, or is retried after a deadlock and finds the other's link). | **Accepted and documented** (slice 4 doc, L6): rare and loud, which is the right way round; `IGNORE` hid it by hiding everything else |

**Checked and clean:**

- **No SQL injection surface in the new code:** no raw or concatenated SQL in Clinical Care (every statement goes through EF's parameterizing interpolated API); the raw calls in the repository are PatientManagement's, older, and parameterized.
- **No clinical content in logs, events or audit rows:** the only logging in Clinical Care is in the existing authentication handlers and retention job; the five new event types and every audit `details` carry ids and field names only (tested per event).
- **The audit trail is complete for every write**, including refusals, and a failing audit insert rolls back its change (tested for create, update, link and unlink).
- **Concurrency:** every update is conditional on the row's stamp and every protected row is a shared one; the legacy-edit-between-read-and-write case is tested against a real database for encounters, problems and allergies.
- **Least privilege:** the grants were rewritten at column level for `lists` and tested from three sides (readable, unreadable, uninsertable/unchangeable); no `DELETE` on `uuid_registry` or `lists`.

**Reviewed and left as they are:**

- `EfProblemUpdater` and `EfAllergyUpdater` (and the two list panels) are close copies. They differ in columns and in wording, and merging them would put a switch on aggregate type into a place that is clearer as two plain statements. Revisit if a third issue type arrives.
- `lists_touch` still uses `INSERT IGNORE`: its only inputs are a validated patient key, a constant type and `NOW()`, none of which can be out of range.
- The eTag is in the body *and* the header. Redundant on a single read, but a list has no per-item header, which is the reason.

**Still open, not fixed here:**

| Item | Why it is still open |
|---|---|
| **The gateway does not authenticate the clinical care routes** (the CRITICAL finding from the patient list review). Verified again: `GET /api/v1/problems` through the gateway answers **200 with no sign-in**, as the existing patient and encounter routes do. | It is the identity phase's job (M5b phase 1b), and it was a decision not to take here. **The new problem, allergy and link routes inherit it.** |
| The audit actor is unknown for updates and links, and caller-named for creates. | Same: needs the verified identity |
| **The new screens have not been run in a browser,** and neither have the encounter screens before them. The WebApp has no automated tests. | Needs a signed-in user and was never possible unattended (pending decisions 7 and 8) |
| **The legacy-UI check:** open an item made here in OpenEMR's own issue screen and encounter screen. | Needs a login; never type the admin password into that page |

## 3. Exit criteria, M5c

| Criterion | State |
|---|---|
| A problem and an allergy can be recorded, read, listed, updated and linked to an encounter, end to end | **Met** (API; the WebApp builds, unverified in a browser) |
| The shadow comparison shows no unexplained discrepancies | **Met** (section 1) |
| Another writer's change is detected, never overwritten | **Met** for updates; legacy overwriting ours is not detectable (ADR 0068) |
| Every write is audited and cannot commit without its audit row | **Met** |
| Only problems and allergies are touched, whatever uuid is sent | **Met**, and now enforced structurally |
| The legacy UI shows what this service wrote | **Not yet checked** (needs a login) |
