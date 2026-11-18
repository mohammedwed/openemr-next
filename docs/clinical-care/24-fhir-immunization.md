# Clinical Care slice: FHIR Immunization, the first of five (M5e)

Decisions are in ADR 0079 (Accepted). This is the first resource built, and the shared plumbing the other four reuse.

## 1. What was built

| Piece | Where |
|---|---|
| `GET /fhir/Immunization/{id}` and `GET /fhir/Immunization?patient=...` | `Api/Endpoints/FhirImmunizationEndpoints.cs`, mapped in a `/fhir` group behind the same authorization |
| The mapper, a pure function of a stored immunization | `Api/Fhir/ImmunizationMapper.cs` |
| Shared: the result type (`application/fhir+json`, weak `ETag`), the `OperationOutcome`s, the strict search parser, the `searchset` Bundle builder | `Api/Fhir/FhirResults.cs`, `FhirSearch.cs` |
| The verified-user middleware now covers `/fhir` as well as `/api/v1` | `Api/Authentication/VerifiedUserMiddleware.cs` |
| `IUserDirectory.FindUuidsAsync`: user id to OpenEMR uuid, for practitioner references | `Application.Ports/IUserDirectory.cs`, `EfUserDirectory.cs` |
| Gateway: `/fhir/Immunization/**` to Clinical Care; Patient Management's `/fhir/**` narrowed to `/fhir/Patient/**` | `Gateway/Program.cs` |
| `Hl7.Fhir.R4 5.11.1` (the version Patient Management pins, ADR 0052) | `Api.csproj` |

## 2. How a stored immunization maps

| FHIR | From | Note |
|---|---|---|
| `id` | the record's uuid | ADR 0079 #2 |
| `meta.versionId`, and the weak `ETag` | the content-hash version, the same token as the JSON API's `ETag` | #5; **no `meta.lastUpdated`**, because the table has no reliable stamp (#4) |
| `meta.profile` | US Core Immunization | |
| `status` | entered in error, then `entered-in-error`; "Completed", then `completed`; anything else, `not-done` | what legacy's service decides |
| `statusReason` | the refusal reason, only for `not-done` | system `v3-ActReason` |
| `vaccineCode` | the CVX code, system `http://hl7.org/fhir/sid/cvx` | with no CVX code, a `data-absent-reason` "unknown" concept, because the element is required |
| `patient`, `encounter` | `Patient/{uuid}`, `Encounter/{uuid}` | |
| `occurrence[x]` | the administered date-time with the **server's UTC offset** (FHIR requires an offset on a time), or the string "unknown" | the JSON API reads these times in the server's zone too |
| `lotNumber`, `expirationDate`, `manufacturer.display`, `doseQuantity`, `site.text`, `route.text`, `reportOrigin.text`, `reasonCode.text`, `note`, `education` | the matching columns | site, route and source are the stored option ids as text, not coded |
| `performer` | administering provider (`AP`) and ordering provider (`OP`) | `Practitioner/{users.uuid}` plus the display name when the user is known (#7); a user with no uuid is named by text only; an ordering provider with neither is left out |

## 3. Search, as decided

`patient` (a bare id or `Patient/{id}`), `_id`, `_count` (default 20, 1 to 100) and `_offset`. A search needs `patient` or `_id`. Anything else is a **400 `OperationOutcome`** that names the parameter and lists what is supported, including `_lastUpdated` (this resource has no stamp), `_sort` and `date`; so is a repeated or empty parameter. A patient with nothing, or that does not exist, is an empty `searchset` (200), not an error. The Bundle has `total`, a `self` link, and a `next` link when there is more; entries are newest administered first, as the JSON list is.

## 4. Limitations

| # | Limitation | Why accepted |
|---|---|---|
| L1 | Route, site and information source are the stored option ids as text, not coded concepts. | Legacy codes them through its own list mappings; this slice does not read those lists. |
| L2 | A practitioner reference dangles: Clinical Care serves no `Practitioner`. | ADR 0079 #7, the user's choice. |
| L3 | The occurrence offset is the server's, not the clinician's. | The same reading as the JSON API (pending decision 9). |
| L4 | No `Immunization` write, no `$validate`, no capability statement. | Read only (ADR 0079 #1 and #10). |
| L5 | `fullUrl` and links use the proto and host the gateway forwarded, which are trusted only because the gateway is the sole caller. | No other caller exists (ADR 0055). |

## 5. Tests (`FhirImmunizationTests`, 32; run and green, with the route and OpenAPI tests: 58)

The US Core check earned its place: it found that an `education` entry needs a document type (`imm-1`), which the first mapping lacked. Mutation check: accepting unsupported parameters, and ignoring the entered-in-error flag, fail 5 tests; restored.


| What it pins |
|---|
| A read is `application/fhir+json` with the record's facts mapped as above; the performer is a reference with a name for a known user and absent for an unknown one |
| The version is the JSON API's own token in `meta.versionId` and as a weak `ETag`, there is no `lastUpdated`, and a correction moves the FHIR version with the JSON one |
| `entered-in-error` beats completion; a refusal is `not-done` with its reason; a completed record has none; an unknown id is a 404 `OperationOutcome` |
| **The output conforms to US Core Immunization** (validated against the vendored packages, read in place from Patient Management) |
| A patient search returns only that patient's records, newest first, with `fullUrl`s and `match` entries; the patient may be bare or a reference; paging follows `next` to the end with no entry twice and none missed; `_id` finds one, and only for the patient named; a patient with nothing or unknown is an empty set |
| **14 refusals**, each a 400 `OperationOutcome` and never a different answer: nothing to search on, a malformed or null patient, a malformed id, `_count` 0, 101 or non-numeric, a negative `_offset`, `date`, `_lastUpdated`, `_sort`, a repeated patient, an empty one; the message names the parameter and what is supported |
| No gateway secret and no verified user are both 401 on read and search; reading writes no audit row and no event |
| Gateway (proxy tests): both paths are protected, reach Clinical Care with the verified user and its own secret and not Patient Management, and `/fhir/Patient` still reaches Patient Management |
