# 0067. WebApp's frontend extends to encounters and vitals

- **Status:** Accepted
- **Date:** 2026-11-17
- **Builds on:** [0061](0061-webapp-covers-insurance-and-advance-directives.md), [0064](0064-clinical-care-shares-the-legacy-database-and-coexists-with-legacy-encounter-writers.md), [0065](0065-vitals-are-stored-in-us-units-and-the-api-takes-explicit-units.md)

## Context

Clinical Care slices 1 to 3 (create an encounter, record vitals, consume `PatientRegistered`) are built, verified on the running stack
and reviewed, but nothing in the WebApp called them. The drawer still carried a disabled "Encounters, Soon" placeholder.

What the API allows shapes the screens more than the legacy UI does:

- **Every encounter list is one patient's.** `GET /api/v1/encounters` answers 400 without a `patientUuid`; there is deliberately no
  "all encounters" read (API-6, unbounded). A cross-patient Encounters page has nothing to call.
- **Both lists are server-paged** (`limit` and `offset`, at most 100 a page), unlike the patient list, which still pages in the browser.
- **There are no lookup endpoints.** Creating an encounter needs a class code, a calendar category id, a facility id and a provider id, and
  nothing returns the valid choices.
- **The caller is not known to the service.** The provider is a number in the request body (finding F-CC-1, ADR 0055).
- **Dates cross a zone boundary.** The API stores server-local time with no zone and, since review finding R-11, converts a date sent *with*
  a zone to the server's local time.

## Decision

1. **Encounters hang off the patient.** The patient page gets an **Encounters** tab (first, with a total in its label); each row opens
   `/patients/{patientUuid}/encounters/{encounterUuid}`, which shows the visit and its vitals. The page refuses an encounter that does not
   belong to the patient in the URL (it would otherwise show one patient's record under another's name). The disabled drawer item and its
   `.op-soon-badge` style are removed; the breadcrumb becomes Patients / Name / Encounter N.
2. **Panels load their own pages.** `PatientEncountersPanel` and `EncounterVitalsPanel` own their page, take a ticket per load so a slow
   earlier response cannot overwrite a newer one, and report a failed page change as a warning over the last good data (the
   `PatientSectionCard` states the insurance and directive panels already use). A ClinicalCare outage therefore never blocks the patient,
   the other tabs, or the sign-in check.
3. **A new encounter opens straight into the visit**, since recording vitals is the next step. Creating and recording are dialogs that close
   only on success, as in ADR 0061.
4. **No reference-data lookups yet.** Class is a select of the 11 legacy `_ActEncounterCode` codes; calendar category and facility are
   numeric fields; the provider id is the same hidden `PlaceholderProviderId = 1` the directive dialogs use. The API still rejects an unknown
   code or facility, and the UI shows the API's own message. Place of service, onset date and sensitivity sit behind "More details".
5. **Numeric fields validate, they never clamp.** `MudNumericField` has a `ConstrainBoundaries` step that pulls a typed value to its `Min`/`Max`
   (found in the assembly; its docs only call them "allowed" bounds, and it was not exercised in a browser here). For a place-of-service
   code or a clinical reading that would save a number nobody entered. Every numeric field uses a validation message instead, worded like the
   API's. Unit-dependent bounds (weight, height, temperature) are left to the API, which converts first and reports every error at once.
6. **One unit switch.** The vitals table and the record dialog each have a single US/metric switch (the API's `?units=` and a per-request
   unit), starting from the units the table is showing. The switch reverts if the converted data fails to load, so it never names a unit
   system the numbers are not in. Values display with one decimal: the service's six-decimal conversion ("175.000001 cm") is noise on screen.
7. **A second error formatter.** `ClinicalCareErrorFormatter` mirrors `PatientManagementErrorFormatter` because each generated client declares
   its own `ApiException` and problem-details types. It also surfaces a 404's `detail`, which ClinicalCare writes for a missing patient.

## Consequences

- Every Clinical Care endpoint except `GET .../vitals/{vitalsUuid}` (nothing needs one record by id) now has a screen.
- **Known limitation, dates and zones.** The generated client types a date as `DateTimeOffset`, so a date always goes out with an offset and the
  API converts it to its own local time. The encounter date is an instant by design, but a **date-only** field (onset date, sent as the
  browser's local midnight) moves to the neighbouring day when the browser's zone and the server's differ across midnight, and the list shows
  the server's digits, not the ones typed. Invisible while both are in one zone, as in the dev stack. Options are in
  `docs/patient-view-pending-decisions.md`, item 9.
- The provider recorded on every encounter and vitals set is `1`, whoever is signed in. Same open gap as the directive changes (item 1 there),
  now covering clinical records.
- The `Encounters` panel's total comes from the API, so the tab label shows the real count, not the 10 rows on screen.
- **Not verified in a browser** and there are no automated WebApp tests (item 7 there). Verified so far: the solution builds with no warnings.

## Alternatives considered

- **A top-level Encounters page with a patient picker.** Rejected: it would duplicate the patient list to reach the one list the API offers.
- **Add facility, category and class lookup endpoints first.** Rejected for now: it is an API change with tests, contract regeneration and
  docs before any screen; the free-form fields work today and the API already validates them. Worth doing, tracked as item 10.
- **Let the numeric fields clamp with `Min`/`Max`, as the insurance dialog does for its company id.** Rejected for clinical values (decision 5).
  The insurance dialog was left alone: a clamped company id of 1 is wrong too, but it is outside this change.
- **Client-side paging of the whole list, as the patient list does.** Not possible: the API bounds every page.

## Related decisions

Extends [0061](0061-webapp-covers-insurance-and-advance-directives.md)'s coverage to Clinical Care, reuses [0055](0055-patientmanagement-api-gated-by-a-shared-secret-identity-forwarding-deferred.md)'s
placeholder-id pattern, and follows [0065](0065-vitals-are-stored-in-us-units-and-the-api-takes-explicit-units.md)'s explicit units.
