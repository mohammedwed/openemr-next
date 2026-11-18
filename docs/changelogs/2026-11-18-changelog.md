# Changelog 2026-11-18

## Added — a patient's demographics can be corrected (M5r, ADR 0081)

- **`PUT /api/v1/patients/{uuid}`** corrects a patient's first and last name, date of birth, sex and emails: the six fields registration writes, under registration's own rules (the same code validates both). It replaces them, so a field sent empty is cleared; the other 120-odd columns of the patient are never written. Chosen by the user as the first, smallest scope.
- **Safe beside legacy.** The version is a hash of the six owned values (`"p-…"`), because `patient_data` has no change stamp the database maintains. It is the `ETag` of `GET /api/v1/patients/{uuid}`. A legacy edit to a name since the read is a conflict (412, never overwritten); a legacy edit to a phone number or address is not. The write is conditional under a row lock, so two simultaneous corrections cannot both succeed.
- **Same contract as every write here:** 401, 428 (no usable `If-Match`), 404, 412 with the current `ETag`, then 400; each refusal audited as failed; one transaction holds the update and its audit row (`patient.update`, field names only); a correction that changes nothing writes nothing; the duplicate score is worked out again afterwards.
- **The patient `GET` now returns sex and trusted email**, which it did not before. A form built on the old response would have cleared both on every save.
- **WebApp:** an Edit button on the patient page and a dialog that starts from the six values as read. A conflict says nothing was saved and reloads.
- **Not built:** `PUT /fhir/Patient` (a full-resource replace would silently drop elements this service does not own), contact details, and a narrowed `UPDATE` grant on `patient_data`. ADR 0081 says why.
- **No database change and no outbox event.**
- **Tests (run, green):** Patient Management unit 188 (22 new), architecture 17, integration 130 (27 new, against a real MariaDB); the whole solution builds in Release with no new warnings; the WebApp component tests still pass (24). The Edit dialog itself is compile-checked only.
- **Mutation check:** removing the version check, writing an extra column, and skipping the re-score each fail tests; restored.
- **A gap the first mutation check found:** removing the *store's* version check was not caught, because the use case checks first and answers 412 before the store is reached (the same thing found for the issue deleter), and even the six-simultaneous test passed by timing. Two tests now call the store directly: one with a stale version, one where a legacy edit lands **between** the read and the write while the correction waits for the row lock. Both pass on the real code and fail with the check removed.
- **My tests had collateral damage, and I fixed them, not the old test:** they gave every patient the same date of birth (1815-12-10) as an existing duplicate-scoring test, which made my patients its duplicates and failed it. My data is now unique per test.
- **A response inconsistency fixed:** the single-patient `GET` returned an empty email as `""` and the `PUT` returned `null`; both are now `null`.

## Fixed — the encounter page crashed on first render (found in the first browser check)

The first real look at the WebApp, signed in on the rebuilt stack, found that **opening any encounter showed "An unhandled error has occurred"**. The browser console named the cause: `More than one sibling of component 'EncounterVitalsPanel' has the same key value`.

- **Cause:** `Pages/Encounters/Detail.razor` keyed all four panels (issues, vitals, SOAP notes, clinical notes) with the bare encounter id. The keys exist so each panel reloads when the route moves to another encounter, but Blazor requires keys to be unique **among siblings**, and these four are siblings. The page therefore threw whenever it rendered them together. It has been there since the M5c and M5d screens.
- **Fix:** each key is now the encounter id paired with the panel's name, so the reload-on-navigation behaviour is unchanged and the keys are unique.
- **Why no test caught it:** those screens were only compile-checked, and the bUnit project covers the delete flows, not this page. That is the case for the browser check, and for a render test of this page (not yet written).
- **Checked, not affected:** the patient page uses the same key pattern, but each panel sits in its own tab, so they are not siblings; its tabs all rendered correctly in the same check.

## Fixed — CI on `main` failed after the stacked merge (`PageRequest` had a public setter)

The stack was merged without waiting for CI on each pull request, so the first full test run of the combined code was the run on `main`, and it failed one test: the Clinical Care architecture rule `Has_no_publicly_settable_property` for `PageRequest`.

- **Cause:** the `_lastUpdated` work (M5q) added `PageRequest.Updated` as `{ get; init; }` and copied the request with `with`. An `init` accessor is a public setter to reflection, and `PageRequest` is one of the types the architecture test requires to be immutable after construction.
- **Fix:** `Updated` is read-only and set only by the private constructor. `Within(window)` builds a new request through that constructor. No caller changed.
- **How it slipped through:** CI had been cancelled on the intermediate merges, and I built the whole solution but did not run the architecture project. A solution build cannot catch a rule like this.
