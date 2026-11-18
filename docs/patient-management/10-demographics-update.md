# Patient Management: correcting demographics (M5r)

Decisions are in ADR 0081; this is the working record.

## 1. What was built

| Piece | Where |
|---|---|
| `PatientVersion` (a content hash of the owned values, `"p-…"`), `PatientDemographics` (the six fields under registration's rules) | `Domain/ValueObjects` |
| `PatientDemographicsView` and `ChangedBy` | `Domain.ReadModels` |
| Port `IPatientDemographicsStore` | `Application.Ports` |
| Use cases `GetPatientDemographicsUseCase`, `UpdatePatientDemographicsUseCase` | `Application/PatientDemographicsUseCases.cs` |
| Adapter: lock, verify the version, write six columns and the table's `date`, audit; one transaction | `Infrastructure/EfPatientDemographicsStore.cs` |
| `PUT /api/v1/patients/{uuid}`, the `ETag` on `GET /api/v1/patients/{uuid}`, 428 and 412 helpers | `Api/Endpoints/PatientEndpoints.cs`, `Preconditions.cs` |
| `EditPatientDialog` and the Edit button | `WebApp/Pages/Patients` |

## 2. Decisions

| # | Decision | Why |
|---|---|---|
| DU-1 | The six fields registration writes, and no others. | The smallest useful scope, chosen by the user. |
| DU-2 | A replacement: a field sent empty is cleared. | One rule; the form always sends all six. |
| DU-3 | The version is a hash of the six values, not a stamp. | `patient_data` has no stamp the database maintains; a hash conflicts exactly when something we would overwrite changed. |
| DU-4 | Verified under `FOR UPDATE` in the correction's own transaction. | A legacy edit between read and write is never overwritten. |
| DU-5 | 401, 428, 404, 412, 400, each refusal audited. | The order every other write uses. |
| DU-6 | Registration's rules, by the same code. | A patient cannot be edited into a state they could not be registered in. |
| DU-7 | A legacy row is read as it is (no date of birth, a short surname) and compared as stored. | A correction must be able to fix a row registration would not have admitted. |
| DU-8 | A no-op writes nothing, audits nothing, keeps the version. | Nothing happened. |
| DU-9 | The duplicate score is recomputed after a change. | Every input to it is one of the six. |
| DU-10 | The single-patient GET carries sex and trusted email. | Otherwise an edit built on it clears both. |
| DU-11 | No outbox event. | F-5; no consumer holds a name; the audit row records it. |

## 3. Limitations

| # | Limitation | Why accepted |
|---|---|---|
| L1 | No `PUT /fhir/Patient`. | A full-resource replace would silently drop elements this service does not own (ADR 0081). |
| L2 | No contact details, address, identifiers or other columns. | Scope chosen by the user; a wider edit needs a decision on ownership. |
| L3 | The service's `UPDATE` grant on `patient_data` is still table-wide. | Narrowing it cannot be tested here (PM's tests do not run the seed script as the least-privilege user). |
| L4 | The Edit dialog is compile-checked only: no component test, nothing seen in a browser. | The WebApp has bUnit tests for the delete flows only. |
| L5 | Re-scoring runs after the commit, as it does after registration; if it fails the caller sees an error for a correction that was saved. | The same as registration (EVT-15 territory); a retry is safe because the version has moved on and the correction is a no-op. |

## 4. Tests

| Layer | What it pins |
|---|---|
| Unit (`PatientDemographicsTests`, 22) | The version is stable, prefixed, moves with any owned value, ignores the public id and the number, cannot be collided by sliding characters, treats empty and none as one; the tag is read from `If-Match` and another kind of tag never matches; a correction refuses exactly what registration refuses with the same words; each changed field is named in a fixed order; a legacy row registration would not admit is still compared as stored |
| Integration (`PatientDemographicsTests`, 27, real MariaDB) | The read carries a strong `ETag` and all six fields; a legacy edit to a phone number does not move the version; a correction answers with the new patient and version; empty fields clear; **only the six columns (and the table's `date`) are written**, with a dozen others compared before and after; a no-op writes no audit row; 428 for none, `*` or blank; 412 with the current `ETag` for a malformed, wrong-kind or stale tag; **a legacy edit to an owned column is a conflict and is never overwritten, to any other column is not**; 404 audited, 428 before it; 400 changes nothing; **six simultaneous corrections succeed once**; a legacy row with no date of birth reads and can be fixed; one success row naming the user and the field names and none of the values; **a correction whose audit row cannot be written does not happen**; **the store itself refuses a stale version, and a legacy edit that lands between the read and the write (the correction waits for the row lock) is caught because the version is checked under the lock**; a correction that makes a patient match another raises their duplicate score; 401 without secret or user |
