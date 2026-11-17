# Changelog 2026-11-17

## Reviewed and fixed — layered review of Clinical Care slices 1 to 3

Full record: `docs/clinical-care/04-layered-review.md`. The review asked one question of every column the service writes: *can the domain
accept a value this column cannot hold?* Ten probes were written first and run against the merged code; **all ten failed**.

- **Fixed (seven defects):**
  - Provider id accepted up to `long.MaxValue` but the column is `int`: a 500 for an encounter, a NULL-user record for vitals (R-1).
  - A tiny height made the derived BMI overflow `decimal(12,6)`: a 500. Height and weight now have floors, and the worst accepted BMI is
    asserted to fit (R-2).
  - A huge number overflowed `System.Decimal` inside the unit conversion before any bound was checked: a 500 (R-3).
  - An event with no `payload` was a 500, which the relay reads as "retry", for an event that can never succeed. Now a 400 (R-4).
  - A nonsense `registeredAtUtc` is stored as unknown instead of as garbage; the patient is still recorded (R-5).
  - `generate-clinicalcare-openapi.sh` had been broken since slice 3 (it did not supply the newly required events secret) (R-6).
  - Encounter and onset dates of year 1 or 9999 were stored in a clinical record. Now bounded to 1900 up to a year ahead, rule CC-17 (R-7).
  - Oxygen saturation is rounded to the column's two places so the value reported is the value held.
- **Tracked (seven):** patient names sent over plain HTTP between containers (R-8); a consumer data conflict is a 500 that blocks the relay
  about 10 minutes before dead-lettering (R-9); Clinical Care's own outbox is never relayed or purged (R-10); offsets on dates are ignored
  (R-11); the provider lookup SQL appears three times (R-12); `EncounterValidationException` is thrown for non-encounter things (R-13);
  a helper on the wrong class (R-14).
- **Reviewed and left as is (three):** wire vocabulary in domain error messages (they are the API's error contract), the early-exit length
  comparison on secrets, and two envelope implementations (design-set decision D4).
- **Clean on inspection:** all 32 SQL call sites are parameterized; no parameter values or clinical text in the logs; no undocumented public type;
  no dead code.
- **Tests:** Clinical Care unit 188, architecture 31, integration 150, all green. The OpenAPI drift guard fired when the provider-id bound
  changed the contract, as designed, and the snapshot and client were regenerated (two added `maximum` lines).
- **Restated, not changed:** the gateway attaches the service secret without checking who the browser user is (ADR 0055). With slices 1 and 2 that
  path carries encounter reasons and vital signs, which is more sensitive than the data the decision was made for.

## Fixed — R-9: a patient conflict at the event consumer is permanent, not a retry loop

- A different patient claiming a pid already held (or the same patient arriving with a different pid) was a **500** at `/internal/events`, which the relay
  treats as transient: one bad event blocked every later one for about 10 minutes before dead-lettering. It is now detected by name inside the
  transaction and answered **422**, which the relay already treats as a rejection of the event itself, so it dead-letters at once. The event is
  not marked processed, so a corrected redelivery still applies.
- The reverse case (same uuid, different pid) was silently accepted as "already known" and is now a conflict too.
- Tests: Clinical Care integration 153 (was 150); the typed exception, both directions, rollback of the processed-event row, a harmless redelivery,
  and the HTTP 422 with its problem+json body.
- Also confirmed: CI passed on PR #78 and its merge commit; the live stack was rebuilt from `main` and the previously-500 inputs return 400 there.

## Fixed — review findings R-10 to R-14

- **R-10, retention.** `clinical_care_processed_events` and `clinical_care_outbox` grew forever. A retention job now removes processed event ids after 30
  days, delivered outbox rows after 30, and never-delivered rows after 365 (long on purpose: nothing consumes them yet and each is derivable from its
  encounter or vitals row). Own tables only, never clinical data, in short batches (bounded to 50 per run), aged by the database's clock. Settings are
  validated at startup. Documented in `docs/configuration.md`, which until now said nothing about PatientManagement or Clinical Care.
- **R-11, date zones.** A date sent with `Z` was stored as its clock digits, a different instant. A date with a zone is now converted to the server's
  local time at the API edge; one without is taken to be local already. The new tests fail without the fix under a non-UTC zone (verified with
  `TZ=Asia/Kolkata`).
- **R-12, provider lookup.** The username-and-group `INSERT ... SELECT` copied three times, and its `'Default'` literal, are now one `LegacyActor`
  helper. Added the test nothing had: a provider in a non-default group.
- **R-13, naming.** `EncounterValidationException` is now `ClinicalCareValidationException`.
- **R-14, placement.** `EncounterReference.TryCreate` replaces an `internal static` helper on a sibling use case.
- **Tests:** Clinical Care unit 207 (was 188), architecture 31, integration 166 (was 150).
- **Still open from the review:** R-8 only (patient names sent over plain HTTP between containers; a contract decision). PatientManagement's own
  delivered-outbox purge (F-PM-3).
- **A flaky test of mine was found and fixed on the way:** `CV_13_the_event_is_in_the_outbox...` asserted that `"154"` did not appear anywhere in the
  whole event envelope, whose random event id, trace id and timestamps can contain those digits by chance (a few percent of runs). It now asserts on
  the payload alone and on the full stored weight (`154.323584`). It had not failed in CI yet; it would have, at random. Three consecutive full runs
  pass, and no other assertion of that shape exists.

## Added — WebApp screens for Clinical Care (encounters and vitals)

Decisions and limits: [ADR 0067](../architecture/0067-webapp-covers-encounters-and-vitals.md). Same look and states as the patient pages
(`PatientSectionCard`, dialogs that close only on success, per-section retry), through the generated `ClinicalCare.Client`.

- **Patient page, new Encounters tab** (first tab, with the total in its label): server-paged list (10 a page), a **New encounter** dialog
  (class, date and time, reason, calendar category, facility, and under "More details" place of service, onset date, sensitivity), and a
  **View** link per row. Creating an encounter opens it.
- **Encounter page** `/patients/{patientUuid}/encounters/{encounterUuid}`: the visit's details, then a **Vitals** card with a server-paged list,
  a **Metric units** switch (the API's `?units=`), and a **Record vitals** dialog. The snackbar reports the BMI the service derived. An encounter
  under another patient's URL is "not found".
- **Layout:** breadcrumb Patients / Name / Encounter N; the disabled "Encounters, Soon" drawer item and its `.op-soon-badge` style are gone.
- **Shared:** `ClinicalCareErrorFormatter` (shows the API's own validation text and a 404's detail), encounter class labels, date, measurement
  and blood-pressure formatting, a wider dialog preset.
- **Deliberate:** numeric fields show validation messages instead of using `Min`/`Max` (a bounded numeric field rewrites a typed value).
  The vitals unit switch reverts if the converted data fails to load.
- **Placeholders, same as the directive dialogs:** the provider id is a fixed `1`; facility and calendar category are plain numeric fields because
  there are no lookup endpoints (`docs/patient-view-pending-decisions.md`, items 1, 9, 10).
- **Dockerfile:** `src/Services/WebApp/Dockerfile` now copies `OpenEmrNext.ClinicalCare.Client`; without it the container build failed on the new project reference.
- **Verification:** `dotnet build` of the WebApp, 0 warnings, 0 errors. **Not yet run in a browser** (needs an OpenEMR login and would write to the
  dev database), and the WebApp still has no automated tests (item 7). Nothing in this change touches the API, the OpenAPI snapshot or the gateway.

## Researched — M5b phase 0: update, delete and audit for Clinical Care

Full record: `docs/clinical-care/05-update-and-audit-research.md`. No code; branch `feat/m5b-update-and-audit`.

- **21 legacy files write `form_encounter`** (encounter form and REST, calendar, HL7, billing, fee sheet, layout forms, merge, delete). "Single
  writer" is false, so updates are conditional on the row's stamp and name only the columns they own (ADR 0068, Proposed).
- **The token already exists:** `last_update` and `last_updated` are `ON UPDATE CURRENT_TIMESTAMP`, kept by the database for every writer. Whole-second
  precision; the one-second residual race is documented.
- **Legacy delete needs `admin/super`**, and a sensitive encounter needs the `sensitivities` ACL. A verified actor is not enough, so delete now
  waits for a permission port. Whole-encounter hard delete (`deleter.php`) is excluded for good because it reaches billing.
- **Legacy vitals edits are in place** with no history and no encounter check; the correction does the same, and the audit row records field names.
- **Audit (ADR 0069, Proposed):** a dedicated `clinical_care_audit` table, INSERT and SELECT only, written in the business transaction.
- **ROADMAP:** M5b added; the plan gained a permission port and moved vitals correction ahead of delete.

## Added — M5b phase 1a, increment 1: the audit port, table and adapter

- **`IAuditLog` and `AuditEntry`** (Application.Ports): an append-only port with no read, update or delete. An entry holds identifiers and
  changed-field *names*; header-sourced text is cut to its column rather than failing a write.
- **`clinical_care_audit`** table and an **`INSERT, SELECT`-only grant** in `scripts/seeds/clinical-care-db.sh` (ADR 0069). The service user cannot
  edit, delete, truncate or drop an audit row.
- **`EfAuditLog`** inserts on the registry's own `DbContext`, so inside a registry transaction the row commits or rolls back with the change.
  Registered in DI; **not yet called** by any use case (next increments: creates, then update).
- **Tests:** `AuditEntryTests` (unit) and `AuditLogTests` (integration: round trip, rollback and commit, no edit or delete, column widths pinned
  against the seed script). Both green.

## Changed — M5b phase 1a, increment 2: creates are audited

- **Every encounter create and vitals record now writes an audit row** (`encounter.create`, `vitals.record`) as the last statement of its
  registry transaction, through `IAuditLog`. A change cannot commit without its audit row, and a failed audit insert rolls the change back.
- **`IEncounterRegistry.RegisterAsync` and `IVitalsRegistry.RegisterAsync` take an `AuditEntry`.** The use cases build it: the actor is the provider
  the caller named, flagged **unverified** until the gateway forwards a session (ADR 0069 #5, F-CC-1); the instant is the same clock reading as the event.
- **`RequestOrigin`** (correlation id and source address) rides on the command as an optional last field, so no use case sees HTTP. The API builds it:
  W3C trace id (else Kestrel's request id) and the first `X-Forwarded-For` entry (else the connection address).
- **Retries stay safe:** a re-run after a lost commit acknowledgement returns before the audit insert, so it audits once.
- **Not yet audited:** refused requests (outcome `failed`: validation, not found) and, later, denied ones. Next increment.
- **Tests, all green (unit 228, architecture 31, integration 186):** use-case unit tests for the audit row and the no-origin case; registry integration tests for
  the row, retry-once and "no commit without the audit row" (a throwing audit log); a use-case integration test; an endpoint test for `X-Forwarded-For`.

## Changed — M5b phase 1a, increment 3: refused creates are audited

- **A refused encounter create or vitals record writes a `failed` audit row** (unknown patient or encounter, validation failure), naming the
  record the caller asked about. Written by the use case as its own statement; the success path is unchanged. **Fail closed:** if that write
  fails, the request is a 500.
- **`AuditEntry` now lets a refusal name the empty uuid** (a caller can send one); a success still must name a real record. Without this, an empty
  patient uuid would have turned a 404 into a 500.
- **`CreateEncounterUseCase` and `RecordVitalsUseCase` take an `IAuditLog`** (a thin wrapper around the existing bodies).
- **Tests, all green (unit 236, architecture 31, integration 192):** unit tests for both use cases (unknown record, validation failure, empty uuid, no
  separate row on success, no number taken by a refusal); integration tests against the real database and through the HTTP endpoints.
- **Phase 1a remaining:** `ETag` / `If-Match` (412, 428) and the update and delete event types.

## Added — M5b phase 1a, increment 4: the concurrency token and `ETag` on reads

- **`ConcurrencyToken`** (Domain): the row's last-update stamp as a **strong `ETag`** (tick count in hex, quoted), plus a strict `If-Match` parser. It rejects
  weak tags, `*`, garbage, and any tick count that is not a whole second we could have issued, so a sloppy header can never make a stale write succeed.
  Sub-second parts are dropped because the column cannot hold them.
- **`EncounterView.LastUpdate` and `VitalsView.LastUpdated`** (optional, so existing constructions are unchanged), selected by both readers. No grant
  change: the service already holds table-level `SELECT`.
- **`GET /encounters/{uuid}` and `GET .../vitals/{uuid}` return an `ETag`** (single-record reads only; lists and 404s carry none). It changes when *any*
  writer changes the row, which the tests prove by editing the row directly as root the way a legacy screen does.
- **Plan change:** `If-Match` handling, 428 and 412 are built **with the first write endpoint (Phase 2)**, where they can be pinned through real HTTP,
  rather than as unused code now. The update and delete event types move into the phases that emit them (2 and 3) for the same reason.
- **Tests, all green (unit 256, architecture 31, integration 196):** `ConcurrencyTokenTests` (unit), an encounter-reader test, and `ETag` tests for both GET endpoints.

## Added — M5b phase 2: update an encounter (`PUT /api/v1/encounters/{uuid}`)

Full record: `docs/clinical-care/06-update-encounter-slice.md` (rules CC-18 to CC-31, limitations L1 to L6). ADR 0068 is now Accepted.

- **The first function beyond create:** replace an encounter's editable set (class, calendar category, date, onset date, reason, facility, place of
  service, provider), conditional on the `ETag` the caller read. 200 with the new `ETag`; **428** without `If-Match`; **412** (carrying the current
  `ETag`) when the record changed, including when a legacy screen or billing action changed it; 400, 401 and 404 as elsewhere.
- **Only owned columns are written.** The database user gets `UPDATE` on exactly nine columns of `form_encounter`; billing, identity, `sensitivity`
  and every unowned column are untouched and cannot be touched (tests assert both the grant and the data).
- **The check happens twice, on purpose:** the use case compares the stamp for a clean 412, and the `UPDATE ... WHERE last_update = ?` closes the race
  a legacy edit would otherwise win. One transaction holds the update, the `EncounterUpdated` event and the `encounter.update` audit row; a failing
  audit insert rolls the update back.
- **No-op updates write nothing** (no event, no audit row). Dates are compared at whole-second precision.
- **Shared rules:** the field rules moved into `EncounterFieldRules`, used by both `EncounterCreation` and the new `EncounterChange`, so a create and an
  update cannot disagree about what is valid. `EncounterResponse` gains `calendarCategoryId`.
- **The actor of an update is recorded as unknown** (verified = 0), by design: the request's `providerId` is whose encounter it is, not who edited it,
  and `form_encounter` has no `user` column. Phase 1b supplies the verified actor.
- **Contract:** the OpenAPI snapshot was regenerated (`generate-clinicalcare-openapi.sh`); the generated client and the whole solution build with no new warnings.
- **Tests, all green on the first run (unit 300, architecture 33, integration 253):** domain (`EncounterChangeTests`), application (`EncounterChangeSetTests`,
  `UpdateEncounterUseCaseTests`), adapter (`EncounterUpdaterTests`), HTTP (`EncounterUpdateEndpointTests`), and revised grant tests.

## Researched — M5c phase 0: problem list and allergies

Full record: `docs/clinical-care/07-problems-and-allergies-research.md`. No code. ADR 0070 (Proposed).

- **Neither is a table of its own.** Both are rows of `lists`, told apart by a free-text, site-extensible `type`, shared with medications, surgeries, dental
  issues and devices. Clinical Care will own exactly `medical_problem` and `allergy`, and every statement filters on `type`.
- **Ten legacy files write it, by routes that disagree:** the issue screen assigns **no uuid** (backfilled later) and stamps `date` with `H:m:s` (the month lands
  in the minutes); REST assigns the uuid and uses `NOW()`, and its `DELETE` is a hard delete that orphans `issue_encounter` rows.
- **The concurrency token already exists:** `lists.modifydate` is `ON UPDATE CURRENT_TIMESTAMP`, the same model as encounters.
- **Resolving a problem is an update** (`enddate`, `outcome`): the UI marks resolved by `enddate`, not `activity`. So the slice needs no delete.
- **Plan:** six slices (problems, allergies, update, encounter link, WebApp, verification); the M5b audit, concurrency and `ETag` machinery is reused.

## Added — M5c slice 1: record, get and list problems

Full record: `docs/clinical-care/08-record-problem-slice.md` (rules PB-1 to PB-20, limitations L1 to L6). ADR 0070 is now Accepted.

- **Three endpoints:** `POST /api/v1/problems`, `GET /api/v1/problems/{uuid}` (with an `ETag` from `lists.modifydate`) and `GET /api/v1/problems?patientUuid=`
  (bounded and paged). The gateway routes `/api/v1/problems` to Clinical Care.
- **A problem is a `lists` row of `type = 'medical_problem'`.** Because that table is shared with allergies, medications and surgeries, **every statement
  filters on `type`**, and the tests prove it from several sides: another type's exact uuid is a 404, and lists exclude other types, other patients and
  rows with no uuid.
- **Column-level grants:** `SELECT` on the 16 columns a problem uses and `INSERT` on 15, plus `SELECT, INSERT` on `lists_touch`. The service cannot read a
  medication's `udi` or write a surgery's `injury_*`; there is no `UPDATE` or `DELETE`.
- **Where legacy's two write paths disagree, the REST path wins:** the uuid is assigned and registered at insert, and `date` is `NOW()` (the issue screen's
  `H:m:s` writes the month into the minutes). The issue screen's `lists_touch` row is also written.
- **One transaction** holds the row, the `uuid_registry` and `lists_touch` rows, the `ProblemRecorded` event (ids only, no clinical content) and the audit
  row; a failing audit insert rolls the problem back, and a retry after a lost acknowledgement writes nothing twice.
- **Refusals are audited** (`failed`, naming the patient asked about), as for encounters.
- **Contract:** OpenAPI snapshot regenerated; the generated client and the whole solution build with no new warnings.
- **A seed-script bug the tests caught:** the new grants' SQL *comments* contained backticks, and that part of the script is an unquoted heredoc, so bash tried to
  run `lists` and `modifydate` as commands. The comments no longer contain any, and the script says why. (The grants themselves were escaped correctly.)
- **Tests, all green (unit 362, architecture 39, integration 330):** domain and use-case unit tests, adapter and grant integration tests, and
  `ProblemEndpointTests` through HTTP. The test fixture now creates `lists`, `lists_touch`, `lists_medication` and `issue_encounter` from the snapshot's own DDL.

## Added — M5c slice 2: record, get and list allergies

Full record: `docs/clinical-care/09-record-allergy-slice.md` (rules AL-1 to AL-14, limitations L1 to L6).

- **Three endpoints:** `POST /api/v1/allergies`, `GET /api/v1/allergies/{uuid}` (with an `ETag` from `lists.modifydate`) and `GET /api/v1/allergies?patientUuid=`
  (bounded and paged). The gateway routes `/api/v1/allergies` to Clinical Care.
- **What differs from a problem:** the begin date is optional; the coded fields are strings (`reaction`, `severity_al`, `verification`) checked exactly against
  three configured lists, with a clean 400 for a configured code longer than its column; `reaction` and `verification` are NOT NULL columns, so an absent one is
  stored as legacy's `''` and reads back as none. A problem's verification list is not the allergy one, and a test proves it.
- **The shared-table rule runs both ways:** a problem's uuid is a 404 as an allergy and an allergy's is a 404 as a problem; lists exclude the other type.
- **Shared code, not copied code:** title, date, diagnosis, comment and provider rules moved into `IssueFieldRules`, and the `uuid_registry` and `lists_touch`
  writes into `IssueRows`, so problems and allergies cannot drift apart. The refactor changed no problem behaviour (362 existing unit tests unchanged).
- **Grants:** `SELECT` and `INSERT` gain `reaction`, `severity_al` and `verification`; still no `UPDATE` or `DELETE`.
- **Found while building it:** a problem also carries a `verification` (list `condition-verification`) that slice 1 did not; recorded as limitation L7 and
  scheduled with problem update. The research doc's allergy `outcome` is dropped (L4).
- **Tests, all green (unit 407, architecture 45, integration 374).** OpenAPI snapshot regenerated; the whole solution builds with no new warnings.

## Added — M5c slice 3: update a problem or an allergy (`PUT`), and problem verification

Full record: `docs/clinical-care/10-update-problem-and-allergy-slice.md` (rules IU-1 to IU-12 and PB-21, limitations L1 to L6).

- **`PUT /api/v1/problems/{uuid}` and `PUT /api/v1/allergies/{uuid}`** replace the editable set, conditional on the `ETag` the caller read (`lists.modifydate`). 200 with the new
  `ETag`; 428 without a usable `If-Match`; 412 (with the current `ETag`) when the record changed, including by the legacy issue screen; 400, 401 and 404 as for encounters. The
  order of checks is the encounter's.
- **Resolving is an update:** send an end date (and usually outcome 1); reopening sends none. The issue screen marks a problem resolved by `enddate`, so no delete is needed to take
  something off the active list.
- **A problem update can never reach another issue type:** every statement carries the type, so an allergy's, a medication's or a surgery's uuid is a 404 on the wrong `PUT`, and the
  tests prove it at the adapter and over HTTP. The database user gets `UPDATE` on exactly eleven columns of `lists`.
- **Problem verification added** (rule PB-21, closing limitation L7 from slice 1): a problem records and updates a `verification` from the `condition-verification` list, which is
  not the allergy list; the tests prove the two never mix.
- **No-op updates write nothing**, as for encounters. The audit actor of an update is recorded as **unknown** by design.
- **Shared, not copied:** `UpdateStatus` and `UpdateOutcome` are now generic across encounters, problems and allergies; the field rules live once per aggregate and serve both the
  record and the change. The refactors changed no behaviour (the existing suites passed unchanged first).
- **Tests, all green (unit 473, architecture 45, integration 443).** OpenAPI snapshot regenerated; the whole solution builds with 0 warnings.

## Added — M5c slice 4: link problems and allergies to an encounter

Full record: `docs/clinical-care/11-link-issue-to-encounter-slice.md` (rules IE-1 to IE-13, limitations L1 to L5).

- **Three endpoints under the encounter:** `PUT /api/v1/encounters/{encounterUuid}/issues/{issueUuid}` (201 new, 200 already there), `DELETE` on the same path (204 either way) and
  `GET /api/v1/encounters/{encounterUuid}/issues` (paged). The gateway already routes the prefix.
- **Stricter than legacy where legacy is unsafe:** legacy's `linkIssueToEncounter` trusts the patient id it is given and never checks the issue is that patient's; here an issue of
  another patient is a 400 (rule IE-3). The reader also refuses to show such a link if legacy made one.
- **Idempotent, as legacy's is, and never updated:** a second `PUT` returns the stored link (and its stored `resolved` flag) and writes, emits and audits nothing. Changing the flag is an
  unlink and a link.
- **Only problems and allergies can be linked:** a medication's, surgery's or any other type's uuid is a 404 (ADR 0070). Orphaned links (legacy's REST delete leaves them) are left out of lists.
- **Unlink removes the link row only,** with an event and an audit row in the same transaction. It deliberately does **not** delete the link's `uuid_registry` row as legacy does, because that
  would need `DELETE` on a table shared by every uuid in the system; a stale registry row is harmless.
- **The actor columns of a new link are null** and the audit actor is unknown, rather than a guessed user id written into a clinical table; fixed when identity forwarding exists.
- **Grants:** `SELECT, INSERT, DELETE` on `issue_encounter`; no `UPDATE`; still no `DELETE` on `uuid_registry`.
- **Tests, all green (unit 494, architecture 49, integration 483).** OpenAPI snapshot regenerated; the whole solution builds with no new warnings.

## Changed — the update contract is now usable by a generated client (`If-Match` and `eTag`)

- **`If-Match` is a declared header parameter on all three `PUT`s** (encounter, problem, allergy), bound by the handler rather than read from the raw request, so the OpenAPI document
  and the typed client both know it. Behaviour is identical: missing, blank or `*` is still 428, an unreadable or weak tag still 412. The three operations also document 412 and 428.
- **`eTag` in the body of `EncounterResponse`, `ProblemResponse` and `AllergyResponse`**, equal to the standard `ETag` header on a single read. A list response has no per-item header, so
  without it a client could not update a row it had just listed without a second `GET` per row.
- **Found by planning the WebApp edit flow:** until now no generated client could have sent `If-Match` at all.
- **Tests, all green (integration 492):** the body `eTag` equals the header on a `GET` and is present on every list item for all three resources, and the OpenAPI document declares the header
  and the 412 and 428 responses on every `PUT`. OpenAPI snapshot regenerated; the whole solution builds.

## Added — M5c slice 5: WebApp Problems and Allergies tabs, and linking on the encounter page

Full record: ADR 0071.

- **Two new tabs on the patient page, Problems and Allergies:** a server-paged list each, a **Record** dialog, an **Edit** dialog, and one-click **Resolve / Reopen** (problems) and **End / Reopen**
  (allergies), all as updates made with the version the row was listed with. A refusal for staleness shows a clear message and reloads; a dialog whose record went stale offers only "Close and reload".
- **The encounter page gains "Problems and allergies addressed":** the linked issues, a **Link a problem or allergy** dialog (the patient's own, minus those already linked, with a resolved-at-this-visit tick),
  and **Unlink**, which asks first and says the issue itself is untouched.
- **Dates:** a calendar date is sent as the same wall-clock time at offset zero, keeping any time of day the row had (`IssueWire`), so one click on Resolve cannot shift a begin date when the browser and
  server zones differ. Exact when the API runs in UTC; recorded under pending decision 9, which it narrows and does not close.
- **Code lists are fixed lists** in `CodeLabels`, as the encounter classes are; the API still rejects an unconfigured code with its message. The provider id is the existing placeholder, for creates only.
- **Not exercised in a browser, and the WebApp still has no automated tests** (pending decision 7): it builds with no warnings, and the API it calls is covered by its own suites. The check needs a re-seeded
  database, rebuilt images and a signed-in user, and is outstanding.
- **Corrected while writing it:** a code comment cited pending decision 10 (lookups) for the time-zone gap, which is decision 9.

## Reviewed and fixed — M5c shadow comparison and layered review

Full record: `docs/clinical-care/12-shadow-comparison-and-review.md`.

- **Shadow comparison on the live stack: 229 columns compared, 215 identical, 14 differ, every difference a documented rule.** Each scenario was run once by OpenEMR 8.0.0's own services and once through the API, for creates
  (both of legacy's write paths for a problem), updates and links. Against the issue screen's own path, a problem is **32 of 32 identical**. The rows were cleaned up and the dev database left as found.
- **Observed live, confirming the research:** the issue screen creates a row with **no uuid** (invisible to this API until backfilled) and stamps `date` with the month in the minutes (`10:22:17` became `10:11:17`).
- **Defect found and fixed (R-15): a link could be written against the wrong encounter.** Encounter numbers reach 4,294,967,295 but `issue_encounter.encounter` is a signed 32-bit column, and `INSERT IGNORE` clamps instead of failing, so
  a valid high-numbered encounter would have been silently linked as number 2,147,483,647. The domain now refuses what the column cannot hold (400), and the insert no longer uses `IGNORE`. Pinned at the domain, HTTP and
  database levels, including a test that shows MariaDB doing the clamping.
- **Structural guard added:** an architecture test reads the SQL and fails the build if any statement on `lists` omits the issue `type` (ADR 0070 #2). It was mutation-tested: removing one filter failed it.
- **Dead code removed:** three audit-action constants nothing used.
- **Still open (not fixed here):** the gateway still serves these routes without a sign-in (the CRITICAL finding, which they inherit); the new screens and the legacy-UI check are not yet verified in a browser.
- **Tests, all green (unit 499, architecture 51, integration 494).**

## Added — M5d slice 1: correct recorded vitals (`PUT /encounters/{uuid}/vitals/{vitalsUuid}`)

Full record: `docs/clinical-care/13-correct-vitals-slice.md` (rules CV-15 to CV-24, limitations L1 to L6). First slice of M5d, "finish Clinical Care", on branch `feat/m5d-clinical-care-completion`.

- **The vitals counterpart of the other updates:** replaces the whole measurement set, conditional on the `ETag` the caller read (`form_vitals.last_updated`); 200 with the new `ETag`, 428 without `If-Match`, 412 (with the current
  `ETag`) when the row changed, including by the legacy screen; 400, 401 and 404 as elsewhere. Either unit system for the response.
- **One set of measurement rules for recording and correcting:** the unit conversion, bounds, whole-number readings, oxygen rounding, text limits and BMI moved out of `VitalsRecording.Create` into `VitalsFieldRules`, which both use; a test
  runs the same inputs through both and demands the same values or the same messages. The extraction changed no recording behaviour (all existing tests passed unchanged first).
- **Reaches only its own vitals:** the `UPDATE` matches through the `forms` registry row on encounter number and patient and excludes soft-deleted vitals, so vitals of another encounter or patient are a 404 even by exact uuid (legacy's own
  statement checks neither).
- **A reading can be removed** by leaving it out (stored as `0`, as legacy reads "not recorded"), at least one must remain, and the BMI is recomputed or cleared with it.
- **Only the 14 measurement columns are written and granted;** who recorded the vitals and when, `authorized` and the `forms` row are untouched.
- **No history, and the editor is unknown,** as for the other updates: the audit row names the measurements that changed, never values.
- **Contract:** `VitalsResponse` gains an `eTag` (also on every list item) and the operation declares `If-Match`, 412 and 428; OpenAPI snapshot regenerated.
- **Tests, all green (unit 533, architecture 53, integration 545).** The solution builds with no new warnings.

## Added — M5d slice 2: immunizations (`/api/v1/immunizations`)

Full record: `docs/clinical-care/15-record-immunization-slice.md` (rules IM-1 to IM-16, limitations L1 to L7), research `14-immunizations-research.md`, decision `docs/architecture/0072-...`.

- **Record, read, list and correct,** the first API that can write legacy's `immunizations` table (legacy's REST and FHIR surface is read-only and its service's write methods are stubs, so the rules are all new). Entered-in-error is a field of the
  correction, legacy's non-destructive void; there is no delete.
- **Versioned by content, not by a stamp** (ADR 0072): the table's `update_date` has no default and no `ON UPDATE`, and the issue screen saves with `REPLACE INTO`, which moves nothing. The `ETag` is a hash of the owned columns (`"c-..."`, a different shape from a
  stamp's), verified under a row lock inside the write transaction, so even a legacy edit landing between the read and the write is a 412. A test fires six concurrent corrections on one version and demands exactly one winner.
- **An expired vaccine is accepted:** giving one is a reportable event, and refusing the record would hide it.
- **Bytes, not characters,** for the two `text` columns; the float `amount_administered` is read and written at four places; a linked encounter must be the same patient's.
- **Grants:** `SELECT, INSERT` plus `UPDATE` on exactly the 24 owned columns (including `update_date`, which this service now sets); no `DELETE`; the patient, uuid, creation and creator columns and `external_id` cannot be written.
- **Contract:** new routes, `ImmunizationResponse.eTag`, `If-Match`/412/428 on the PUT; OpenAPI snapshot regenerated.
- **Tests, all green (unit 616, architecture 59, integration 623).** The solution builds with only the 7 pre-existing warnings.

## Added — M5d slice 3: SOAP notes (`/api/v1/encounters/{uuid}/soap-notes`)

Full record: `docs/clinical-care/17-soap-notes-slice.md` (rules SN-1 to SN-10, limitations L1 to L6), research `16-soap-notes-research.md`, decision `docs/architecture/0073-...`.

- **Record, read, list and correct** an encounter's SOAP notes. `form_soap` has no uuid and no stamp, so a note is addressed by its legacy id **within its encounter** (ADR 0073) and versioned by a content hash of its four sections (ADR 0072), the same
  `ETag`/`If-Match`/412/428 contract as the immunizations.
- **Safer than legacy's own REST update:** every statement matches the note through its encounter's `forms` row, so a note of another encounter or patient is a 404 (legacy's update checks neither); a correction does **not** rewrite `date` or `pid`, which legacy's does.
- **The legacy rows now say who wrote them:** the acting provider's username and group are recorded (legacy's REST path leaves them empty).
- **Bytes, not characters,** for the `text` sections; an entirely empty note is refused.
- **Audit gains a keyed subject:** `AuditEntry` can name a record that has no uuid (`soap:{id}`), written into the same column.
- **Grants:** `SELECT, INSERT` on `form_soap` plus `UPDATE` on exactly the four sections; no `DELETE`; date, pid, user, group, authorized and activity cannot be written.
- **Refactor:** the content-versioned update outcome is now one shared `ContentUpdateOutcome` (immunizations and SOAP).
- **Tests, all green (unit 650, architecture 63, integration 671).** The solution builds with only the 7 pre-existing warnings.

## Added — M5d slice 4: encounter lookups (`/api/v1/lookups/...`)

Full record: `docs/clinical-care/18-lookups-slice.md` (rules LK-1 to LK-6, limitations L1 to L3). The API half of decision 10 in `patient-view-pending-decisions.md`.

- **Three read-only lists:** `encounter-classes`, `facilities` and `calendar-categories`, so a caller can offer a choice instead of typing ids. Categories are the ones legacy's own visit form offers (active patient-visit categories, not no-show, in legacy's order); facilities are the
  non-inactive ones. Every list is bounded at 500 rows, and what they offer is what creating an encounter accepts (a test does exactly that).
- **No providers list on purpose:** staff names over an API reachable without sign-in would widen that open finding; the acting provider should come from the session once identity is forwarded.
- **Grants:** `SELECT` on `inactive` and `service_location` of `facility`, and on six columns of `openemr_postcalendar_categories`; nothing else about either table is readable and neither is writable.
- **Not yet done:** the WebApp still shows its fixed class list and numeric category and facility fields.
- **Tests, all green (unit 650, architecture 63, integration 692).** The solution builds with only the 7 pre-existing warnings.

## Changed — M5d: lookups wired into the WebApp, and the gateway now routes immunizations and lookups

- **Create-encounter dialog:** class, visit category and facility are now selects filled from the lookups (class defaults to legacy's default, category to the first configured one, facility to the only one when there is exactly one). If a lookup cannot be loaded the
  dialog warns and falls back to the previous fixed class list and plain id fields. Not browser-verified.
- **Bug fixed: the gateway had no route for `/api/v1/immunizations` or `/api/v1/lookups`.** Both were served by Clinical Care and built with passing tests, but 404ed for every browser, because the gateway lists each prefix it forwards and its own tests run only against a live stack. Both routes added.
- **Guard:** `GatewayRoutesTests` (Clinical Care suite) compares every API prefix in the OpenAPI document with the gateway's route list, so a new route group cannot be built and left unreachable. Verified to fail without the fix.

## Added — M5d slice 3b: clinical notes (`/api/v1/encounters/{uuid}/clinical-notes`) — M5d is complete

Full record: `docs/clinical-care/19-clinical-notes-slice.md` (rules CN-1 to CN-10, limitations L1 to L6), decision `docs/architecture/0074-...`.

- **Record, read, list and correct** an encounter's clinical notes. Unlike SOAP, `form_clinical_notes` has a uuid and a real stamp, so identity and the `ETag` follow vitals; the new difficulty is that **one parent `forms` row groups several note rows** whose `form_id` legacy allocates as an unlocked `MAX+1`.
- **Each note gets its own parent form, its id allocated under a database lock** (ADR 0074). A test with eight simultaneous writers found a real bug in the first version: the transaction's snapshot was fixed before the lock was acquired, so waiting writers still read the stale maximum and took the same id.
  The lock is now taken on an explicitly held connection before the transaction begins. It serialises this service's writers; legacy's own writer takes no lock, so that race is narrowed, not closed.
- **The code and label are derived from the type** (the LOINC code in the option's `notes`), so a note cannot carry a code that disagrees with its type. A note is reached only through its own encounter, matching both its own columns and its parent's `forms` row, and only while active; retiring a note (`activity = 0`) is a delete and is not offered.
- **Two lookups:** `/lookups/clinical-note-types` (with each type's LOINC code) and `/lookups/clinical-note-categories`, the active options in legacy's order.
- **Grants:** `SELECT, INSERT` plus `UPDATE` on exactly six columns of `form_clinical_notes`; `SELECT` on two more `list_options` columns (`notes`, `activity`). The existing test that pinned `list_options.notes` as unreadable was replaced deliberately, with the columns still denied listed instead.
- **Not covered:** linked documents and results; `note_related_to` is not parsed (can be stale after a text correction).
- **Tests, all green (unit 685, architecture 69, integration 760).** The solution builds with only the 7 pre-existing warnings.

## Added — M5d: WebApp screens for vitals correction, immunizations, SOAP notes and clinical notes

Full record: `docs/clinical-care/20-webapp-clinical-screens.md`. **Compile-checked only: the WebApp has no automated tests and none of this has been opened in a browser (it needs a signed-in user).**

- **Vitals:** a "Correct" button per row; the dialog re-reads the record in US units (as stored) and saves against the fresh `ETag`.
- **SOAP notes and clinical notes:** cards on the encounter page with record and edit. Clinical note type and category are selects from the lookups; a retired type stays selectable on a note that has it.
- **Immunizations:** a new tab on the patient page with record and edit, coded fields as selects, and "Entered in error" to void an entry.
- **A silent data loss prevented:** a correction replaces the whole set, and the immunization dialog does not show the linked encounter, ordering provider or the user id of who gave it, so saving would have cleared them. `ImmunizationResponse` now returns `encounterUuid` and the dialog sends all three back; a test pins keeping and clearing the link.
- **API:** `GET /api/v1/lookups/immunization-options` (seven titled lists in one call, active only, legacy's order) and `encounterUuid` on the immunization response; OpenAPI snapshot regenerated.
- **Tests, all green (unit 685, architecture 69, integration 765).** The solution builds with only the 7 pre-existing warnings.

## Security — M5b phase 1b, part 1: the gateway now requires a signed-in session for Clinical Care and Patient Management (ADR 0075)

**Closes the critical finding that the Clinical Care and patient APIs were reachable through the gateway with no sign-in.** Until now the gateway attached each backend's shared secret to every request for its routes, so anyone who could reach the gateway could read and write clinical data.

- **The gateway asks AuthFacade** (`GET /api/v1/me`, forwarding only the caller's cookie) before forwarding any request to a Clinical Care or Patient Management route (including `/fhir`). No session is **401** and nothing is forwarded; AuthFacade down or answering oddly is **503** (fail closed). AuthFacade's own revocable session store means a revoked session stops working on the next request; the check is not cached.
- **The verified user is forwarded** to those two services as `X-Authenticated-User` (the OpenEMR user uuid). The gateway deletes any caller-supplied value of that header on every route, and no longer forwards the browser's session cookie to the two data services.
- **Tests:** a new in-process project, `OpenEmrNext.Gateway.ProxyTests` (26 tests), runs the real gateway and real YARP proxying against fake backends: 401 without a session on every protected prefix, a revoked or odd session, 503 when the sign-in service errors or is unreachable, a forged identity header replaced or ignored, only the cookie sent to the check, case-insensitive path matching, no check for sign-in or the static app, and revocation taking effect at once. With the check switched off, 21 of the 26 fail. The live-stack tests are updated to expect 401 and pass (14) against the rebuilt gateway.
- **Not yet done (part 2):** Clinical Care does not use the forwarded identity yet; the acting provider and audit actor are still caller-named. External FHIR clients now need a session; a bearer-token path is a separate decision.

## Security — M5b phase 1b, part 2: Clinical Care acts as the verified user (ADR 0075)

**Closes finding F-CC-1 (the acting provider was whatever the caller named) and the placeholder provider in the WebApp.** With part 1, Clinical Care is now closed to anyone without a signed-in session and records who really did each thing.

- **Every `/api/v1` route needs the verified user** the gateway forwards (`X-Authenticated-User`): none or malformed is **401**, a uuid matching no user or an account legacy has deactivated is **403**. It is middleware, so it runs before any body is bound: a first version as an endpoint filter answered a bad body with 400 before telling an anonymous caller to sign in, which a test caught.
- **The verified user is the acting provider and the audit actor** (`Verified = true`, subject their uuid, id `users.id`). Every audit row, success or refusal, record or correction, names a real user: `unknown` and `user:{caller-named}` no longer appear from HTTP. Event envelopes carry the same actor.
- **A `providerId` in a request body is ignored** and is now optional on all seven record requests (an encounter's own provider, a field of the visit, defaults to the acting user and can differ from them). Legacy rows (`user`, `groupname`, `created_by`) name the signed-in user. The OpenAPI document and snapshot are updated.
- **Grant:** `users` readable columns are now `id`, `username`, `uuid`, `active` (still no credential or contact columns); tests pin what stays denied and that nothing is writable.
- **WebApp:** no clinical dialog sends a provider id any more. Patient Management's dialogs still send the placeholder, because it has no uuid-to-id lookup yet (it is now behind the sign-in requirement, but its caller-named identity fields are unchanged).
- **Tests, all green: Clinical Care unit 688, architecture 69, integration 798; gateway proxy 26; live-stack gateway 14.** The Clinical Care test client acts as a seeded user (`dr_who`, id 7); 33 new tests cover 401 and 403 on every route, the verified actor in audit and events, a second user as a different actor, the body `providerId` being ignored, and the grants. The live stack is rebuilt and re-seeded.
- **Not verified in a browser** (needs a signed-in user): that the WebApp's calls carry the session through the gateway and succeed.

## Security — M5b phase 1b, part 3: Patient Management acts as the verified user too (ADR 0075)

**Closes decision 1 in `patient-view-pending-decisions.md`: no more hardcoded user id `1` anywhere in the WebApp.**

- **Same rule as Clinical Care:** every `/api/v1` and `/fhir` route needs the verified user the gateway forwards (401 if none or malformed; 403 if it matches no user or an account legacy deactivated), as middleware that runs before any body is bound. Patient Management reads `users` itself (`id`, `uuid`, and the newly granted `active`; never the credential columns).
- **The verified user is who registers a patient** (`patient_data.created_by`, also for `POST /fhir/Patient`), **owns an uploaded advance-directive document** and **reviews the directive status**. The `RegisteredBy`, `OwnerId`, `AuthenticatorUserId` and `?registeredBy=` a caller names are ignored and now optional.
- **WebApp:** the registration, document upload and directive status dialogs no longer send a user id (the last three uses of the placeholder `1`).
- **Not built, on purpose: a bearer-token path for external FHIR clients.** `/fhir` now needs a session, and nothing in this repository calls it from outside; the design depends on which clients exist (see ADR 0075 #7).
- **Tests:** Patient Management unit 166, architecture 17, integration 86 (12 new, covering 401 and 403 on every route, the verified user recorded whatever the body says, FHIR create, and the secret still being required). PM OpenAPI snapshot regenerated; the live stack is rebuilt and re-seeded and the live gateway tests pass (14).

## Fixed — the gateway's session check read the wrong field, so every signed-in user looked signed out (ADR 0075)

**Found by the user the first time the new sign-in check met a real session** ("when I navigate to patients it sees me as logged out"). The gateway asked AuthFacade `GET /api/v1/me` correctly and got a 200, but then looked for a `sub` property; AuthFacade's `MeResponse` calls it **`subject`**. Every real session was therefore treated as not signed in and refused with 401 before reaching Patient Management or Clinical Care. Anonymous requests were refused correctly, so nothing was ever exposed; it failed closed.

- **Why the tests missed it:** the in-process gateway tests used a fake AuthFacade, and I wrote the fake with the same wrong guess as the code, so they agreed with each other and not with the real service. The live-stack tests only cover requests with no session, which needs no real field name. The browser check I had flagged as outstanding is exactly what would have caught it.
- **Fix:** the gateway reads `subject`, named by one constant. The fake uses that constant, and two new contract tests read AuthFacade's published OpenAPI snapshot and assert that `MeResponse` has that property and does not have `sub`. Reintroducing the bug fails both.
- **Lesson recorded:** a fake of another service must be derived from that service's published contract, not from the author's memory of it. The gateway proxy tests are now 28 and pass; the gateway is rebuilt and restarted.

## Added — providers lookup (M5f)

- **`GET /api/v1/lookups/providers`** lists the active, authorized users by name, for naming a provider other than yourself on an encounter. It is routed through the gateway like the other lookups and needs a signed-in session. Details and decisions PV-1 to PV-6: `docs/clinical-care/18-lookups-slice.md` section 5.
- **Clinical Care's grant on `users` widens from four columns to seven** (`fname`, `lname`, `authorized` added), as decided by the user. Credential and contact columns stay unreadable, and the grant tests were rewritten to pin the exact set.
- **WebApp:** the create-encounter dialog gets a Provider select (default "Me (signed-in user)"), and the encounter page shows the provider's name instead of a number.
- **Tests:** Clinical Care unit 688, architecture 69, integration 805, all green; the build has no warnings. The OpenAPI snapshot is regenerated. The live Docker stack has not been rebuilt for this yet.

## Added — permission port and deleting records (M5g, ADR 0076)

- **`DELETE` on vitals, SOAP notes and clinical notes** (`/api/v1/encounters/{encounterUuid}/vitals/{vitalsUuid}`, `.../soap-notes/{noteId}`, `.../clinical-notes/{noteUuid}`), answering 204. A deletion is **legacy's own flag**, never a removed row: `forms.deleted = 1` for vitals and SOAP notes (what legacy's `delete_form.php` does), `activity = 0` for a clinical note. Legacy's screens, and this service's readers, already hide a flagged record.
- **Only a user who holds legacy's `admin/super` right may delete.** A new port, `IAccessControl`, reproduces `aclCheckCore` over legacy's `gacl_*` tables in one parameterized query (direct and group rules with the nested-set inheritance, a refusal beats an allow, disabled and extended-object rules ignored, a superuser holds everything). Nothing is cached, so a right removed in OpenEMR stops the next deletion.
- **Order of answers:** 401, 428 (no `If-Match`), **403**, 404, 412. A user who may not delete is refused before anything is looked up, so a 403 reveals nothing about which records exist. Every refusal after sign-in is audited as failed.
- **Conditional and safe under races:** the record is locked, its version (stamp, or content hash for a SOAP note) is verified in the same transaction, and the flag, the outbox event and the audit row commit together. Six simultaneous deletes of one record succeed once.
- **Grants:** `UPDATE (deleted)` on `forms`; `activity` added to the clinical-note update columns; read-only, column-level access to nine `gacl_*` tables. The three grant assertions that pinned those as refused were changed on purpose, and the rest of the boundary is pinned tighter than before.
- **WebApp:** a Delete button on each vitals, SOAP note and clinical note row, with a confirmation, one shared helper (`RecordDeletion`) that explains each refusal (403 "only an administrator can delete", 404 and 412 reload the list), and a step back a page when the last row of a later page goes. Compile-checked only.
- **Not done, decision needed:** problems and allergies. You chose a soft delete hidden from lists, but legacy's issue screens do not filter on `activity`, so such an issue would vanish here and still appear in OpenEMR; legacy itself hard-deletes. Waiting for your call (ADR 0076, "Not decided").
- **Tests and verification:** Clinical Care unit 704, architecture 69, integration 887, all green; `AccessControlTests` and `RecordDeletionTests` run against the real legacy DDL as the production principal. **Mutation check:** with the permission check and the version check disabled, 11 tests fail; both restored.

## Added — idempotency keys and the SOAP retry guard (M5h, ADR 0077)

- **`Idempotency-Key` on every creating `POST`** under `/api/v1` (optional, so no caller changes). A retry with the same key and the same request is answered with the first answer (`Idempotent-Replayed: true`) and creates, audits and emits nothing more; the same key with a different request is 422; while the first is still running it is 409 with `Retry-After`. Scoped to the verified user. Only success is kept, so a refused request releases its key. Answers live 24 hours in the service's own `clinical_care_idempotency` table; a claim held over five minutes is taken over. Closes F-CC-8.
- **A SOAP note no longer duplicates when its transaction is re-run after a lost commit acknowledgement.** A note has no identity before it is inserted, so each attempt now gets an id written into the `SoapNoteRecorded` event (new, additive `attemptId`) in the same transaction; a re-run looks for it first. The lookup runs only on a re-run. Closes limitation L2 of the SOAP slice. The test loses a real commit acknowledgement.
- **Bug the tests caught on first run:** the claim token column was `CHAR(36)`, which the MySQL driver reads as a `Guid`, so every first request with a key was a 500. It is `VARCHAR(36)`.
- **Not done:** the WebApp does not send keys; the OpenAPI document does not declare the header (it would change every generated client signature); the other services have none.
- **Database:** one new table owned by the service, created by the seed script (re-run it on an existing database); no legacy table touched.
- **Tests:** Clinical Care unit 722, architecture 69, integration 911. Mutation check: with the key claim and the SOAP guard disabled, 11 tests fail; both restored.

## Added — the first automated WebApp tests (M5i)

- **New project `tests/Services/WebApp/OpenEmrNext.WebApp.UnitTests`** (bUnit 2.15, NSubstitute; in the solution, so CI runs it). It renders the real panels with MudBlazor's real dialog and snackbar and a scripted client: no browser, no Docker, about a second.
- **24 tests** for the delete flows of the vitals, SOAP and clinical note panels: confirm sends the row's own `ETag` and reloads; "Keep it" sends nothing; 403, 404, 412 and a network failure each say the right thing and reload or not as they should.
- **Mutation check:** making the 403 handling claim success fails 3 tests, sending a wrong `ETag` fails 4; both restored.
- **Not covered:** everything else in the WebApp, and anything that needs a real browser. The browser check of all screens is still outstanding.

## Added — Patient Management's audit trail (M5k, ADR 0078)

- **Every Patient Management write now leaves an audit row** in its own append-only table, `patient_management_audit`: who (the verified user's uuid and `users.id`, marked verified), what, which record, the outcome, the trace id, the caller's address, and the names of the fields changed, never a value. Registering a patient (API and FHIR), adding and swapping insurance, uploading an advance directive document and setting the directive status are covered.
- **In the write's own transaction, as its last statement:** a change cannot commit without its audit row, and if the row cannot be written the change does not happen (tested for registration, insurance, documents and status). A refusal (a registration, policy or document the domain rejects, a swap of an unknown policy) leaves a `failed` row.
- **One behaviour change:** setting an advance directive status for a patient that does not exist is **404** (it used to be a silent 204 that changed nothing), because a no-op cannot be audited as a success. The OpenAPI document and its snapshot declare the 404.
- **Not covered, on purpose:** reads of patient data (a separate access log), and legacy's own `log` table. The PM tests connect as the container owner, so the append-only grant is not exercised there.
- **Database:** one new table created by the seed script (re-run it on an existing database); no legacy table touched.
- **Tests:** Patient Management unit 166, architecture 17, integration 103 (17 new, `AuditTrailTests`; the existing ones were updated for the new constructors), all green. Mutation check: removing the in-transaction audit append from the registry and the insurance add fails 6 tests; restored. One `DuplicateScoringTests` failure appeared once in the first full run and did not recur in three more; not diagnosed.

## Fixed — abandoned AuthFacade sessions are no longer kept for ever (M5l)

- **The leak:** an expired session row was deleted only when that same session's cookie was presented again. A browser closed for good, or a lost device, left its row in `Sessions` for ever, so the table only grew. Found while reading the code for the gateway's per-request check (ADR 0075 amendment).
- **The fix:** `AuthFacadeSessionStore` sweeps expired rows when a **new** session is created: up to 100 per sign-in, oldest first, keys read then deleted by key (so it works on both SQLite and MariaDB). No background job; a backlog is worked off over the next sign-ins. A session with no expiry is never swept; a live one is never touched; the existing "expired cookie is refused and removed" path is unchanged.
- **Tests:** the session store had none. 5 new unit tests run the real store on a real in-memory SQLite database (an abandoned session is removed, a live and a no-expiry one are kept, one sign-in sweeps a bounded batch and the rest go on the next, renewing does not sweep, an expired cookie is still refused). AuthFacade unit 86, all green. Mutation check: removing the sweep call fails 2 tests; restored.
- **No database change:** the table is unchanged.

## Added — FHIR Immunization, the first clinical FHIR resource (M5e slice 1, ADR 0079)

- **`GET /fhir/Immunization/{id}` and `GET /fhir/Immunization?patient=...`**: read-only FHIR R4, mapped with the Firely SDK as Patient Management's `Patient` is. Status follows legacy's rule (entered in error, then completed, else not-done, with the refusal reason); the CVX code is the vaccine code; people are `Practitioner/{uuid}` references with a display name; `meta.versionId` and a weak `ETag` carry the same token as the JSON API's; there is **no** `lastUpdated`, because the table has none.
- **Search is strict, as decided:** `patient`, `_id`, `_count` (1 to 100) and `_offset` only; anything else, or a repeat, is a 400 `OperationOutcome` that names the parameter. A patient with nothing, or unknown, is an empty set. The Bundle has `total`, `self` and `next`.
- **The gateway's `/fhir/**` is now split.** It all went to Patient Management; now `/fhir/Patient/**` does, and `/fhir/Immunization/**` goes to Clinical Care. Both need a signed-in session, and a test pins each path to the right service. Clinical Care's route test learned the `/fhir` prefix.
- **Shared plumbing for the next four resources:** the result type, the search parser, the Bundle builder, `OperationOutcome`s, an id-to-uuid user lookup, and the verified-user middleware now covering `/fhir`.
- **No database change.**
- **Tests (run, green):** the 32 new `FhirImmunizationTests`, the route test and the OpenAPI tests (58 in that set), and the gateway proxy tests (32, 3 of them new). The full Clinical Care suites were not run on this change.
- **Found by the US Core check:** an `education` entry needs a document type (`imm-1`) and the first mapping gave only dates; fixed ("Vaccine Information Statement" when there is a VIS date, "Patient education" otherwise).
- **Mutation check:** accepting unsupported search parameters, and ignoring the entered-in-error flag, each fail tests (5 in all); restored.
