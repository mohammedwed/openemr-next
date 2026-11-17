# 0078. Patient Management keeps its own append-only audit trail

- **Status:** Accepted
- **Date:** 2026-11-17
- **Builds on:** [0069](0069-clinical-care-audits-writes-to-its-own-append-only-table.md), [0075](0075-the-gateway-validates-the-session-and-forwards-the-verified-user.md)
- **Closes:** the open question in `docs/patient-management/05-workflows-and-events.md` ("should the audit trail be the events, or a separate audit log?") and the "Audit" gap table in `06-data-ownership-and-legacy-mapping.md` for writes.

## Context

Patient Management records who registered a patient, owns a document or reviewed a directive in legacy's own columns (ADR 0075), but nothing recorded **who changed what and when**: an insurance policy added or swapped left no trace at all, a directive status overwrote the last one, and the only evidence for registration was a caller's id in `created_by`. Clinical Care had solved this with its own table (ADR 0069); Patient Management had not. The outbox cannot be the record: its rows are marked published and purged, and only registration produced an event at all.

## Decision

1. **A dedicated, append-only table owned by this service**, `patient_management_audit`, with the same shape as Clinical Care's: when, who (the verified user's OpenEMR uuid, their `users.id`, and whether the gateway verified them), what action, which record, the outcome, the trace id, the caller's address, and the **names** of the fields changed. Never a value, so a leaked audit table is not a leak of patient data.
2. **Append-only by privilege.** The service's database user is granted `INSERT` and `SELECT` on it and nothing else; the retention decision is left to operations and is not made by this service.
3. **Written in the write's own transaction, as its last statement**, for the five writes: register a patient (through the API and through FHIR), add an insurance policy, swap insurance, upload an advance directive document, and set the directive status. A change cannot commit without its audit row, and if the audit row cannot be written the change does not happen.
4. **A refusal is audited too**, as a `failed` row: a registration, policy or document the domain rejects, a swap of a policy that does not exist, a status for a patient that does not exist. Nothing was changed, so the row is its own statement; its subject is the record asked about, or a short key (`patient:42`, `unregistered`) when there is none yet.
5. **The actor is the verified user** (ADR 0075). A use case called without an origin (a test, a job) records `unknown`, unverified, rather than inventing one.
6. **Subjects.** A record with a uuid is named by it (patient, insurance policy, directive document). A patient addressed by its number (the directive status) is named `patient:{pid}`.
7. **One behaviour change:** setting an advance directive status for a patient that does not exist used to answer 204 and change nothing. It is now **404**, because a write that changes nothing cannot be audited as a success. The OpenAPI document declares the 404.
8. **Not decided here, on purpose:**
   - **Reads of patient data are not audited.** Who looked at which chart is a separate access log with its own volume and retention questions; it needs its own decision.
   - **Legacy's `log` table is not written.** Whether legacy's own paths write there was never verified; patients created here may be missing from legacy's audit screens. Still open.
   - **Only registration emits an event.** Insurance and directive changes still produce no outbox row (F-5 in the workflows doc); the audit row is what records them now.

## Consequences

- Every Patient Management write is attributable and cannot silently skip its record, matching Clinical Care.
- One new table owned by the service, created by the seed script (re-run it on an existing database); no legacy table touched.
- The test database for Patient Management is built by hand and connects as the container's owner, so **the append-only grant itself is not exercised by its tests** (Clinical Care's suite runs the real seed script as the least-privilege user). The table definition in the tests is taken from the seed script's own text so it cannot drift, but a revoked or widened grant would not be caught here. Recorded as a limitation.
