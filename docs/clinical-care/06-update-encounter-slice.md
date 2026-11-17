# Clinical Care M5b slice: update an encounter

- **Milestone:** ROADMAP M5b, phase 2. **Decisions:** [ADR 0068](../architecture/0068-clinical-care-updates-are-conditional-on-last-update-and-touch-only-owned-columns.md) (the concurrency model), [ADR 0069](../architecture/0069-clinical-care-audits-writes-to-its-own-append-only-table.md) (audit). **Research:** [05](05-update-and-audit-research.md).
- **Endpoint:** `PUT /api/v1/encounters/{uuid}` with `If-Match` (a declared header parameter, so a generated client can send it; the current tag is also in the response body as `eTag`). **Status:** built; tests in section 7.

## 1. What it does

Replaces an encounter's **editable set** (class, calendar category, date, onset date, reason, facility, place of service, provider) with what the
caller sends, if and only if the record is still the version the caller read. It returns the encounter as stored, with its new `ETag`.

| Outcome | Status | When |
|---|---|---|
| Updated | **200** + `ETag` | The version matched; at least one field changed |
| Nothing to change | **200** + the current `ETag` | The version matched but the body equals what is stored. No write, no event, no audit row |
| Invalid input | **400** | Any field rule fails (all violations listed together, DOM-3) |
| Not found | **404** | No such encounter, or legacy has soft-deleted it (CC-9) |
| Changed since read | **412** + the current `ETag` | The offered tag is not the current one, including a tag that is unreadable or weak |
| No precondition | **428** | `If-Match` is missing, blank or `*` |
| No gateway secret | **401** | As every endpoint (ADR 0055) |

The order is fixed:

1. **400 for a malformed body**, decided at the binding boundary before the handler runs (a missing class or date, a number out of range): the attribute
   checks on `UpdateEncounterRequest`.
2. **428**, if `If-Match` is missing, blank or `*`.
3. **404**, if there is no such encounter.
4. **412**, if the offered tag is not current.
5. **400 for a domain rule** (an unknown class or facility, a date out of range).

So a caller that is out of date is told so whatever domain rule its request breaks, and a caller that never said which version it was working from is
told nothing about the record, not even whether it exists.

## 2. Rules (continuing the catalogue in `01-create-encounter-slice.md` section 3)

| Id | Rule | Legacy source | Parity | Where |
|---|---|---|---|---|
| CC-18 | An update replaces the whole editable set. Nothing defaults: a missing **class** or **date** is an error, not the list default or now. | `EncounterService::updateEncounter` writes every posted column | **(new)** `PUT` semantics on a shared row; the legacy form always posts every field | `EncounterChange.Create` |
| CC-19 | The update applies only if the row's `last_update` still equals the version the caller read; otherwise nothing is written. | none: legacy is last-writer-wins | **(new)**, LEG-10, ADR 0068 | `EfEncounterUpdater` |
| CC-20 | A write without `If-Match` (or with `*`) is refused. An unconditional update is never allowed. | none | **(new)** | `Preconditions.TryRead` |
| CC-21 | If nothing differs from what is stored, nothing is written, no event is emitted and no audit row is written. | legacy `UPDATE` runs anyway and MariaDB does not bump `last_update` for an identical row | Parity in effect | `EncounterChangeSet`, use case |
| CC-22 | Only the owned columns are in the statement: `date`, `onset_date`, `reason`, `class_code`, `pc_catid`, `facility_id`, `facility`, `pos_code`, `provider_id`. The database user holds `UPDATE` on exactly these. | the form also posts `billing_facility`, `referral_source`, `in_collection`, ... | Narrower than legacy **on purpose**: those columns have other writers and no slice owns them | `EfEncounterUpdater`, seed script |
| CC-23 | The same field rules as a create (class in the list, category positive, bounds on date and onset, reason length, provider range, facility exists, place of service 1 to 99). | `EncounterValidator` | One implementation shared with `EncounterCreation` (`EncounterFieldRules`) | `EncounterFieldRules` |
| CC-24 | The facility's name is written with the facility id; the place of service defaults from the facility when not given. | `updateEncounter` resolves `facility` from `facility_id` | Parity | `EncounterChange.Create` |
| CC-25 | The `forms` registry row is **not** touched. | legacy's update does not touch it either | Parity | `EfEncounterUpdater` |
| CC-26 | `sensitivity` is **not editable** here. | legacy refuses a sensitive encounter without the `sensitivities` ACL | Deferred: needs the permission port (F-CC-2) | not in `EncounterChange` |
| CC-27 | An encounter whose `newpatient` form is soft-deleted is not updatable (it reads as absent). | CC-9 | Parity with the UI | `EfEncounterUpdater`, reader |
| CC-28 | Dates are cut to whole seconds before they are compared or written, so a resubmitted date with a stray fraction is not a change. | the columns hold whole seconds | **(new)** | `EncounterChange.Create` |
| CC-29 | A blank reason is `NULL` or `''`: the two are the same reason when deciding whether anything changed. | legacy stores either | Tolerant read (DOM-9) | `EncounterChangeSet` |
| CC-30 | One transaction holds the conditional `UPDATE`, the `EncounterUpdated` outbox row and the audit row. If the audit insert fails, the update does not commit. | none | **(new)**, SEC-9, EVT-1 | `EfEncounterUpdater` |
| CC-31 | The event and the audit row name the **fields** that changed, never their values. | none | **(new)**, EVT-5, SEC-9 | use case |

## 3. Who is the actor?

The audit row for an update records `actor = unknown`, `verified = 0` (`AuditActor.Unknown`). That is deliberate, not an omission. The request's
`providerId` says **whose encounter it is after the change**, and `form_encounter` has no `user` column, so legacy does not record who edited either.
Treating `providerId` as the actor would put a wrong name in the audit trail. Phase 1b (identity forwarding) supplies the verified actor.

## 4. Concurrency, as built

- The token is `form_encounter.last_update` (`timestamp`, `ON UPDATE CURRENT_TIMESTAMP`, whole seconds), sent as a strong `ETag` (`ConcurrencyToken`).
- The use case reads the row and compares its stamp with the offered tags (fast, gives a clean 412 with the current tag). The adapter then runs
  `UPDATE ... WHERE uuid = ? AND last_update = ?`, which is what actually closes the race: a legacy edit landing between the read and the write moves
  `last_update`, matches zero rows, and surfaces as a 412 too. The two checks are not redundant.
- **Zero rows matched** (not "changed": MySqlConnector counts matched rows, pinned by a test) means the stamp moved or the row vanished; one more read
  says which.
- **The protection is one-sided.** A legacy screen saving after us still overwrites our values without warning, and legacy's form posts columns we do
  not own. Nothing here can change that; it is stated so nobody assumes otherwise.

## 5. Known limitations (stated, not hidden)

| # | Limitation | Why accepted |
|---|---|---|
| L1 | **Whole-second stamp.** Two writes in the same second as the caller's read are indistinguishable. After an update inside the same second as the previous stamp, the new `ETag` equals the old one. | It is the best token legacy rows have (ADR 0068); a version column would be ignored by legacy |
| L2 | **A lost commit acknowledgement is reported as 412 on retry,** never a second write. The retry finds the stamp already advanced by its own first attempt. | A create is recognised by its uuid; an update has no such key. The event and audit row commit once |
| L3 | **Actor unknown** until Phase 1b (section 3). | No verified identity exists yet |
| L4 | **`sensitivity` cannot be changed** and is not enforced when reading or updating (F-CC-2). | Needs the permission port |
| L5 | **DST fall-back hour:** a `timestamp` compared as local time can repeat for one hour a year. | Legacy runs with the same assumption (LEG-11); the window is one hour and one second wide |
| L6 | **No history.** An update overwrites; the audit row says which fields changed, not the old values. | Parity with legacy (research doc, finding 5) |

## 6. Deferred, with the precondition (KD-8)

| Item | Precondition |
|---|---|
| Soft-delete an encounter | The permission port (legacy needs `admin/super`), phase 3b |
| Edit `sensitivity` | The permission port, phase 3b |
| `encounter_type`, referral, discharge, billing facility fields | A code-list port and a slice that owns them |
| Link issues to the encounter | A problem-list context |

## 7. Tests

| Layer | What it pins |
|---|---|
| Unit (domain) | `EncounterChange`: every field rule, nothing defaults, whole-second dates, all violations together |
| Unit (application) | `EncounterChangeSet` (one field at a time, null versus empty reason); the use case against fakes: order of checks, no-op writes nothing, conflict at write time, event and audit content, refusals audited |
| Integration (adapter) | Every owned column changes; unowned columns survive; a legacy edit between read and write is detected; soft-deleted and vanished rows; identical values still match; **a failing audit rolls the update back**; the grants (`UPDATE` on exactly the owned columns, denied elsewhere) |
| Integration (HTTP) | 200, 400, 401, 404, 412, 428 and the `ETag` round trip; wildcard, blank, weak, garbage and listed tags; zone-bearing dates; audit and event rows; billing and sensitivity untouched |
