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
