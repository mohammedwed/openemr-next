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
