# Patient Management: the audit trail (M5k)

Decisions are in ADR 0078; this is the working record.

## 1. What was built

| Piece | Where |
|---|---|
| `AuditEntry`, `AuditActor`, `AuditOrigin`, `AuditActions`, `IAuditLog` | `Application.Ports/AuditEntry.cs`, `IAuditLog.cs` |
| Adapter: one parameterized `INSERT` into the current transaction | `Infrastructure/EfAuditLog.cs` |
| Table `patient_management_audit` and its `INSERT, SELECT` grant | `scripts/seeds/patient-management-db.sh` |
| The success row, built by each write use case and appended **inside** the repository's transaction | `EfPatientRegistry`, `EfInsurancePolicyRepository` (add, swap), `EfAdvanceDirectiveRepository` (upload, status) |
| The refusal row, appended by the use case when the domain refuses | the five use cases, `Application/Audit.cs` |
| The origin of a request (verified user, trace id, forwarded address) | `Api/Endpoints/AuditOrigins.cs` |

## 2. The five audited actions

| Action | Subject | Fields |
|---|---|---|
| `patient.register` | the patient's uuid (`unregistered` on a refusal) | none |
| `insurance.add` | the policy's uuid (`patient:{pid}` on a refusal) | none |
| `insurance.swap` | the policy's uuid | none |
| `directive.document.upload` | the document's uuid (`patient:{pid}` on a refusal) | none |
| `directive.status.set` | `patient:{pid}` | `["status"]` |

## 3. Limitations

| # | Limitation | Why accepted |
|---|---|---|
| L1 | Reads of patient data are not audited. | A separate access log with its own decisions (ADR 0078 #8). |
| L2 | Legacy's `log` table is not written. | Whether legacy's own paths write there was not verified. |
| L3 | Only registration emits an outbox event; insurance and directive changes do not. | F-5 in `05-workflows-and-events.md`; the audit row records them now. |
| L4 | The test database connects as the container's owner, so the append-only grant is not tested. | PM's fixture predates the real-seed-script approach Clinical Care uses; moving to it is its own change. |
| L5 | A lost commit acknowledgement that is retried can leave two identical audit rows for one change (the write itself is not duplicated by the audit). | The same as Clinical Care; a row too many is better than a row too few. |
| L6 | Setting a directive status for a patient that does not exist is a 404 now (it was a silent 204). | A no-op cannot be audited as a success (ADR 0078 #7). |

## 4. Tests (`AuditTrailTests`, 17)

| What it pins |
|---|
| Registering through the API writes one success row naming the verified user (uuid, id, verified), the patient, the forwarded address (the first of `X-Forwarded-For`) and a trace id; through FHIR the same |
| Adding and swapping insurance are each audited under the policy; uploading a document and setting the status are audited; the status row lists `["status"]` |
| A status for a missing patient is 404 and a `failed` row, with no success row |
| A request with no verified user writes nothing and leaves no row; reading is not audited |
| Refusals: a registration the domain rejects, an invalid policy, a swap of an unknown policy and a rejected document each leave a `failed` row and no data |
| **Atomicity:** with an audit log that cannot write, no patient or event, no policy, no document and no status change is committed |
| The trail holds identifiers and field names and none of the patient's name, date of birth, email, policy or group number |
| Over-long header values are cut to the column, never refused |
