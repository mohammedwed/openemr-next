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

## Fixed — every delete in the WebApp reported failure when it had worked (found in the browser check)

Deleting a vitals set from the encounter page showed **"Could not delete this record. Nothing was deleted."**, while the database showed the row flagged deleted and Clinical Care's log showed the request answered `204 No Content` with its audit row and outbox event written.

- **Cause:** the OpenAPI document declared only `200` for five delete endpoints (vitals, SOAP notes, clinical notes, problems, allergies) because they had no `.Produces(...)`. They return `204`. The WebApp's client is generated from that document, and a generated client throws on any status the document does not declare, so every successful delete raised an exception that the screen put into words as a failure. The unlink-from-encounter endpoint declared its `204` and was unaffected. The same applied to M5g and M5n: no delete from the WebApp has ever reported success.
- **Fix:** the five endpoints declare `204`; the snapshot is regenerated.
- **A second flaw it exposed:** the screen said "Nothing was deleted" for any failure it had no special case for, including a 5xx or a dropped connection, where the request may have committed. Those now say the outcome is unknown ("may or may not have happened") and read the list again, as the 404 and 412 cases already did.
- **Why no test caught it:** the panel tests script the client, so they never met the real generated one. New tests use the real `ClinicalCareClient` against a stub that answers 204, for all five deletes (with the old snapshot all five fail, which is the bug reproduced), and a Clinical Care test now asserts every `DELETE` in the document declares `204` and not `200`. Also new: a panel test for 5xx and the updated network-failure wording.
- **Seen once and not explained:** the first failed delete showed an HTTP 503 in the browser's network list, although Clinical Care and the gateway both logged a 204. Two later deletes through the gateway returned 204 on the wire. I could not reproduce the 503, and have not found where it came from, so it is open.
- **Checks:** WebApp unit 70 passed, Clinical Care OpenAPI document tests 26 passed, whole solution builds with 0 errors. The Clinical Care delete integration suites were not rerun (the endpoint code is unchanged apart from its declared metadata).

## Changed — times in the WebApp are UTC (ADR 0083), and a new patient's records no longer look like an outage

**The time bug.** A user in Cairo (UTC+2) opened New encounter, which proposed 06:30, created it, and the page showed 04:30. The dialog attached the browser's offset to the local time, Clinical Care stored the UTC equivalent, and the pages show stored digits without converting back. The project owner decided: **times are UTC**.

- A default time comes from the UTC clock (an injected `TimeProvider`, so a test can pin it); a time typed into a dialog is sent as the same digits at a zero offset (`UtcTime.ToWire`); a time on a page is shown as stored with a ` UTC` suffix; the time pickers say "Time (UTC)".
- Date-only values (date of birth, policy dates, onset and effective dates) are sent as midnight at a zero offset, so the browser's zone can never move one by a day; "today" for the date pickers and the age on the patient page is the UTC date.
- This depends on the stack's clocks being UTC, and they are (checked: the database, both services and legacy's PHP default; legacy's `gbl_time_zone` is empty). ADR 0083 records the condition.
- Tests: the encounter dialog is opened with a clock pinned at 04:30 UTC and must propose and send 04:30 at a zero offset; helper and display tests. Under `TZ=Africa/Cairo` the old code fails the dialog test (both the old clock and the old offset, separately). On a UTC machine, as in CI, only the clock half can fail.

**The registration race.** Right after registering a patient, the Encounters, Problems, Allergies and Immunizations sections said "the clinical care service may be unavailable". Clinical Care learns of a new patient from the registration event a few seconds later and answers 404 until then, which is not an outage. Those sections now say "Clinical records for this new patient are still being set up", offer "Check now", and retry by themselves every 3 seconds up to 5 times (the shared `PatientSectionCard`); any other failure still reports an unavailable service and is never retried automatically.
- Tests (six): a 404 says "still being set up" and not "unavailable", a 500 or 503 still says "unavailable", the section appears once Clinical Care knows the patient, the card retries by itself but only up to the limit, and a plain failure is never retried. Removing the cap, or the 404 recognition, fails the right tests.
- **Checks:** WebApp unit 83 passed (also under the Cairo clock), whole solution builds with 0 errors and 0 warnings.

## Changed — patient searches are bounded pages, and `LIKE` text is matched as typed (ADR 0082)

An audit of how both services pass values to SQL found **no SQL injection path**: about 145 interpolated calls are parameterized by EF, 24 `SqlQueryRaw` calls use numbered placeholders, the one direct `MySqlCommand` uses a named parameter, and the only runtime-built SQL text (the `IN (…)` lists and the patient search filter) is made of placeholders and literal column names. It did find two weaknesses in Patient Management's patient search, both now fixed:

- **Unbounded search.** `GET /fhir/Patient` had no limit, so no parameters (or `family=%`) loaded every patient. It is now a page: `_count` (1 to 100, default 25), `_offset`, a `total` of everything that matched, `self` and `next` links, and a 400 for a bad value. The order is by `pid`, which is unique, so paging is stable. The plain `GET /api/v1/patients` takes `limit` and `offset` (default and maximum 100) and returns the total in `X-Total-Count`; the WebApp's patient list says when it is showing the first 100.
- **Wildcards in `LIKE` text.** `%` and `_` in `family`, `given`, `name` and the address parameters acted as wildcards. They are escaped, with an explicit `ESCAPE '!'`.
- **Tests:** 26 new (`PatientSearchBoundsTests`): paging walked through every `next` link, the range checks, wildcards matched only as themselves, nine search parameters sent quote, `DROP TABLE`, `UNION` and `SLEEP` text (each matches nothing and a known patient is still found), and the plain list's limit and total. A mutation check (no escaping; no `LIMIT`) failed the right tests. The Patient Management suites pass: unit 188, architecture 17, integration 156 including these.
- **Not changed, and said so in the ADR:** this service still ignores unknown search parameters (Clinical Care answers 400), and still drops an unparseable `_lastUpdated` or `birthdate` silently.

## Found in the legacy-UI check (the records written here, as OpenEMR shows them)

With the project owner signed in to OpenEMR, the scratch patients' records were opened in OpenEMR itself. Problems, immunizations (CVX codes resolved to names), encounters and the SOAP and vitals deletions all appear as expected, with three exceptions, none of them caused by this service:

- **OpenEMR's dashboard still showed deleted vitals as the "most recent vitals".** Its summary query joins `forms` to `form_vitals` on `form_id` alone, with no `formdir = 'vitals'`, so a set whose id matches another form's id is counted as not deleted (four deleted sets with ids 3 to 6 matched four `newpatient` rows of other patients). The flag is set exactly as OpenEMR's own delete sets it, so a delete made in OpenEMR shows the same. Our confirmation said "no longer shown here or in OpenEMR", which was untrue for that card; it now says the encounter forms stop listing the set and the dashboard may still show it (EncounterVitalsPanel, `21-permissions-and-delete.md` L7).
- **"No Known Allergies"** appears after an allergy is recorded and deleted, because `lists_touch` (legacy's "reviewed" marker) is left behind, as legacy leaves it (`23-delete-problems-and-allergies.md` L3).
- **A deleted clinical note leaves a "Clinical Notes Form" line** in OpenEMR's encounter list, as OpenEMR's own delete does (L8).
- **Two input oddities I first suspected in the WebApp were the user's own keystrokes** in the shared browser (an extra letter, and a trailing 0 deleted from a CVX code). Neither reproduced and neither is an app bug.
- **Browser check, now covered:** register, edit and save (version check: 200, 412, 428, 404 through the gateway), create encounter, record, correct and delete vitals, SOAP note, clinical note, problem, allergy and immunization. Not yet: insurance, advance directives, and the immunization correct and entered-in-error flows.

## Docs — README and ROADMAP brought up to date; a DDD design set and UML for Clinical Care

- **README:** the status table (Clinical Care built, the gateway checks the session and forwards the verified user, the WebApp covers the clinical screens), new paragraphs for Patient Management and Clinical Care, the stale known gaps replaced with the current ones, the repository structure, the test commands with last results, and links to both services' design sets. The gateway's route description was corrected against its code (an `/api` catch-all for AuthFacade, outranked by the specific service routes).
- **ROADMAP:** M4, M5 and M5b status, the M5e and M5b headings, a new M5s section for the Nov 18 merge, browser check, legacy-UI check, SQL audit and the defects they found, and a rewritten "what remains before the post-M5 gate".
- **`docs/clinical-care/design/`:** bounded context, ubiquitous language, context map, domain model, workflows and events, data ownership and legacy mapping, and 19 UML diagrams, written from the code and the seed script.
- **How it was checked:** every claim was read from the code, and that caught wrong ones in the first draft (the project references in the dependency diagram, a conflict that answers 422 not 409, and several rule descriptions). Every Mermaid diagram was run through Mermaid 11.4.1's own parser in a browser, which found three syntax errors (a semicolon inside a sequence message, twice, and an extra colon in a state label), now fixed. I also found that the architecture tests use plain reflection rather than ArchUnitNET, which the README had implied.
- **Things the set says plainly that were easy to miss:** Clinical Care writes an outbox event with every change but has no relay and nothing consumes them (unpublished rows are purged after 365 days); and the `Api` project is the composition root, with `Composition` holding only the options classes.
