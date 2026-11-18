# 0081. A patient's demographics are corrected under a content-hash version of the columns this service owns

- **Status:** Accepted
- **Date:** 2026-11-18
- **Builds on:** [0068](0068-clinical-care-updates-are-conditional-on-the-version-the-caller-read.md), [0072](0072-immunizations-are-versioned-by-a-content-hash-because-the-table-has-no-stamp.md), [0075](0075-the-gateway-validates-the-session-and-forwards-the-verified-user.md), [0078](0078-patient-management-keeps-its-own-append-only-audit-trail.md)
- **Closes:** the first half of P4-1 in `docs/patient-management/07-gaps-and-evolution.md` (demographics update with a concurrency token), the "no update" capability gap, and PT-11 (no concurrency token).

## Context

Patient Management could register a patient and never correct one: a typo in a name or a date of birth could only be fixed in OpenEMR's own screens. The gap analysis planned an update behind a concurrency token and held it until two things existed, a verified user (ADR 0075) and an audit trail (ADR 0078). Both do. The user chose, on Nov 18, the smallest useful scope: **the six fields registration writes** (first and last name, date of birth, sex, email, trusted email), not contact details.

What makes this different from the Clinical Care corrections:

- `patient_data` has about **130 columns** and many writers. A correction must write only the columns this service owns.
- It has **no change stamp the database maintains**. Its `date` column is set by whoever writes, so it cannot be trusted as a version (ADR 0072 met the same thing for immunizations).
- Legacy rows hold whatever its screens let someone type: a missing date of birth, a one-letter surname. Reading a row must never be refused for what it holds.

## Decision

1. **`PUT /api/v1/patients/{uuid}` replaces the six fields.** The body is the six, under registration's own rules (the same code validates both, so a patient cannot be edited into a state they could not have been registered in). A field sent empty clears it, and the other 120-odd columns are never written.
2. **The version is a hash of the six owned values** (`"p-"` and 32 hex digits, `PatientVersion`). It changes exactly when something this service would overwrite changes: a legacy edit to a name is a conflict, a legacy edit to a phone number or an address is not. It is returned as the `ETag` of `GET /api/v1/patients/{uuid}` and in an `eTag` property, and the public patient id and the patient number are not part of it.
3. **The write is conditional on it, under a row lock**: the row is read `FOR UPDATE` in the correction's own transaction and the offered tags compared with the version read there, so a legacy edit between the caller's read and the write is a conflict and two simultaneous corrections cannot both succeed.
4. **The order of answers is the one every write here uses:** 401 (not signed in), 428 (no usable `If-Match`; `*` is not usable), 404 (no such patient), 412 (the patient changed since the read, with the current `ETag`), then 400 (a field fails the rules). Each refusal after sign-in is audited as failed.
5. **One transaction** holds the update and its audit row (`patient.update`, listing the names of the fields that changed and never their values), as every write here does. A correction that changes nothing writes nothing: no audit row, the same version.
6. **The duplicate score is worked out again** after a correction that changed something, as it is after registration, because every input to it (name, date of birth, sex, email) is one of the six.
7. **The single-patient `GET` returns all six fields, including sex and trusted email**, which the summary did not carry. A form built on the old response would have cleared them on every save, because the correction replaces all six; the response now carries what an edit has to send back.
8. **No outbox event.** Only registration publishes one (finding F-5), no consumer holds a name (Clinical Care's patient reference is a uuid and a number), and the audit row records the change. A `PatientUpdated` event waits for a consumer that needs it.
9. **The WebApp** has an Edit button on the patient page and a dialog that starts from the six values as read and is based on that read's version; a conflict closes it, says nothing was saved, and reloads.

## Not decided here, on purpose

- **`PUT /fhir/Patient`.** P4-1 planned it, and it is **not built**. A FHIR `PUT` is a full replacement of a resource that carries race, ethnicity, address, telecom and a dozen extensions this service does not own and would have to drop or ignore; either is a silent loss. It needs its own decision on what an update to an element we do not own should mean.
- **Contact details, address, identifiers and the rest of the 130 columns.** A wider edit needs a decision on which legacy columns this service owns and how legacy populates them.
- **Narrowing the service's `UPDATE` grant on `patient_data`** (it is table-wide today) to the columns it writes. Worth doing; not done here because Patient Management's tests do not run the seed script as a least-privilege user, so a narrowed grant could not be tested.

## Consequences

- A registered patient can be corrected in the new system, safely alongside legacy's own screens.
- The patient `GET` gains an `ETag` and three response properties; clients regenerate.
- No database change.
