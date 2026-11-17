# Patient view: pending decisions for the architect

Items from the patient list and patient view review that need an API change, a cross-service decision, or
an approval before work can start. Items that did not need any of those were done in the webapp and are
recorded in `docs/changelogs/2026-11-16-changelog.md`.

Each item says what is blocked, what it costs, and the options. Nothing here has been implemented.

## 1. Who is recorded as making a directive change

**Blocked:** every advance-directive status change and document upload is sent with a hardcoded user ID of `1`
(`PlaceholderUserId` in `SetAdvanceDirectiveStatusDialog.razor` and `UploadDocumentDialog.razor`). The audit
trail therefore names the wrong person for every change.

**Why it needs a decision:** the signed-in user from AuthFacade's `GET /api/v1/me` has a string `subject` and a
`name`, but no numeric OpenEMR user ID. The API expects the numeric ID (`AuthenticatorUserId`, `OwnerId`).

**Options:**
- Map `subject` to OpenEMR's user ID in AuthFacade or PatientManagement, and return it from `/api/v1/me`.
- Have PatientManagement resolve the caller from the session, instead of trusting a client-supplied ID.

**Cost:** an API change in AuthFacade or PatientManagement, plus a client regeneration.

## 2. Document upload size

**Blocked:** the webapp caps uploads at 20 MB. The Domain allows 30 MB (`AdvanceDirectiveDocument.MaxContentBytes`).
Documents travel as base64 inside JSON, which adds about a third. ASP.NET Core's default request limit is about
30 MB, which I have not confirmed in this environment.

**Options:**
- Raise the request limit for the upload route in PatientManagement, then raise the webapp cap to match the Domain.
- Change the upload to multipart or streamed bytes, which avoids the base64 overhead.

**Cost:** an API change, and a contract change if multipart is chosen.

## 3. Patient detail beyond the summary

**Blocked:** the Overview shows only name, patient ID, date of birth, and email. `GET /api/v1/patients/{uuid}`
returns `PatientSummaryResponse`. The read model (`PatientFhirView`) already holds address, phones, sex, race,
ethnicity, language, and gender identity, but only `GET /fhir/Patient/{id}` returns them.

**Options:**
- Add a `PatientDetailResponse` to `GET /api/v1/patients/{uuid}`. This was implemented once and reverted at the
  owner's request. Exclude SSN.
- Have the webapp call the FHIR endpoint and parse the response, either by hand or with the `Hl7.Fhir.R4` package
  the backend already uses. The package adds to the WebAssembly download size, which has not been measured.

**Cost:** option 1 is an API change and a client regeneration. Option 2 is a webapp dependency and a size check.

## 4. Medical record number (MRN)

**Blocked:** clinicians may identify patients by MRN. The API returns the public patient ID (`pubpid`) but no
separate MRN. The service does not model one.

**Decision needed:** whether `pubpid` is the MRN in practice, or whether a separate MRN concept must be modeled.

## 5. Patient list: server-side paging and search

**Blocked:** `GET /api/v1/patients` returns the whole list, and the webapp pages and searches it in the browser.
This will not scale as the patient count grows.

**Options:** add `page`/`pageSize` (or cursor) parameters and a total count, and move name matching to the server.

**Cost:** an API change, and the webapp's paging and search would switch to server data.

## 6. Generated client: optional query parameter typed as non-nullable

**Blocked:** `GetPatientsAsync(string name)` is generated as non-nullable, so the webapp passes `null!` for "no
filter". The OpenAPI snapshot does not mark the query parameter optional or nullable.

**Cost:** mark the parameter nullable in the API's OpenAPI output, then regenerate the client
(`scripts/dev/generate-patientmanagement-openapi.sh`).

## 7. Automated tests for the webapp

**Blocked:** the webapp has no test project. Everything in the patient view has been checked by hand in the browser.

**Needs approval:** adding a test project (for example bUnit) and running `dotnet test`. Per the owner's
preference, tests are not run without asking first.

## 8. Browser checks that change dev data

**Blocked:** these are not yet verified in the browser, because each one would change the dev database:
- A successful dialog submit (add policy, swap, set status, upload).
- The count refresh after a successful change.
- The upload size rejection.
- A single section failing while the patient loads.

**Needs approval:** permission to submit changes in the dev stack.

## 9. Dates that cross a time zone (encounters)

**Blocked:** the ClinicalCare client types every date as `DateTimeOffset`, so the webapp always sends an offset, and the API (review finding R-11)
converts a date with an offset to the **server's** local time. For the encounter date that is correct (an instant). For a **date-only** field
(onset date, sent as the browser's local midnight) it moves to the neighbouring day when the browser and the server are in different zones, and the
encounter list shows the server's digits rather than the ones typed. Invisible while both are in one zone, as in the dev stack.

**Options:**
- Send date-only fields without an offset: a string-typed `date` in the OpenAPI schema (or `DateOnly`), then regenerate the client.
- Decide that clinical dates are the server's local time everywhere and show a zone label next to them in the webapp.

**Cost:** the first is an API contract change plus a client regeneration; the second is a UI change only.

## 10. Lookups for the encounter form

**Blocked:** creating an encounter takes a class code, a calendar category id, a facility id and a provider id, and nothing returns the valid
values. The webapp offers the 11 legacy class codes from a fixed list, plain numeric fields for category and facility, and a placeholder provider
(`1`, see item 1). The API still rejects an unknown class or facility, so a wrong entry fails safe, but the user has to know the ids.

**Options:** add read endpoints to ClinicalCare for classes (`_ActEncounterCode`), facilities and calendar categories (the service already reads
the first two through `IEncounterCodeLists` and `IFacilityDirectory`), and, once identity is forwarded, resolve the provider from the session.

**Cost:** API endpoints with tests, an OpenAPI snapshot and client regeneration, then selects in `CreateEncounterDialog`.

