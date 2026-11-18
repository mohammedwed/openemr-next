# Patient Management: idempotency keys

- **Status:** Built and tested. Decision: [ADR 0084](../architecture/0084-patient-management-creating-requests-accept-an-idempotency-key.md), a port of Clinical Care's design ([ADR 0077](../architecture/0077-creating-requests-accept-an-idempotency-key-and-soap-notes-guard-their-own-retries.md), `docs/clinical-care/22-idempotency.md`).
- **Closes:** finding F-2 (no idempotency) and P1-6 for the creating routes. **Does not close** F-1 (see section 4).

## 1. What it does

A creating `POST` under `/api/v1` may carry an `Idempotency-Key` header: **register a patient, add an insurance policy, swap insurance.** The same key with the same request, after the first one finished, returns the first answer again and creates nothing. Without the header nothing changes.

| The request | The answer |
|---|---|
| Same key, same request, first one **finished** | The first answer again, marked `Idempotent-Replayed: true` |
| Same key, same request, first still **running** | 409, `Retry-After: 5` |
| Same key, **different** request (method, path, query or body) | 422 |
| Malformed key, or two keys | 400 |
| Body over 1 MB | 413 |
| A key on the **document upload** | 400: "A document upload cannot carry an Idempotency-Key." |

The key belongs to the verified user (ADR 0075), so one user's key cannot replay another's answer. Only a 2xx answer is kept; a refused or failed request releases its key so a corrected retry can reuse it.

## 2. How it works

| Piece | Where |
|---|---|
| The rules (key shape, fingerprint, window, lease) | `Application.Ports/Idempotency.cs` |
| The store port | `Application.Ports/IIdempotencyStore.cs` (`ClaimAsync`, `CompleteAsync`, `ReleaseAsync`) |
| The adapter | `Infrastructure/EfIdempotencyStore.cs`, on `patient_management_idempotency` |
| The middleware | `Api/Authentication/IdempotencyMiddleware.cs`, after `VerifiedUserMiddleware` |
| The table and grant | `scripts/seeds/patient-management-db.sh` (the service holds all four privileges on it) |
| The WebApp | the Register, Add policy and Swap insurance dialogs begin an *attempt*; `IdempotencyKeyHandler` turns it into the key; it skips the upload route |

- **Atomic by primary key.** A request inserts its claim with a fresh token and reads the row back; it owns the key only if the token is its own. Of any number of simultaneous requests with one key, exactly one runs.
- **Lease.** A claim not completed within five minutes is presumed dead and may be taken over.
- **Window.** Answers are kept 24 hours; expired rows are swept as a side effect of claiming.
- **Privacy.** The table holds a SHA-256 of the request and the answer (identifiers, status, location), never the request body. A test pins that a patient's name does not appear in it.

## 3. Why the upload is excluded

The directive document upload carries its file as base64 in a JSON body of up to about 30 MB. The middleware fingerprints a request by reading its whole body, capped at 1 MB, so an upload cannot be fingerprinted. Ignoring a key there would let a caller think a retry was safe, so the route refuses it with a clear 400, and the WebApp's handler never adds one.

## 4. Limits

| # | Limit | Why accepted |
|---|---|---|
| L1 | **F-1 is still open:** a registration that commits and then fails (scoring runs after the commit, in the endpoint) answers 500, which releases its key, so a retry registers a second copy | Closing it means moving scoring out of the request, a separate change |
| L2 | A request that dies between committing a record and saving its answer leaves a claim that expires after the lease; a retry may then duplicate | The same window Clinical Care accepts (ADR 0077 #7) |
| L3 | The service's own transaction retry (a lost commit acknowledgement) is not guarded the way a Clinical Care SOAP note is | Registration has no attempt id in its outbox event yet |
| L4 | `PUT`, `POST /fhir/Patient` and the upload are not covered | `PUT` is already conditional on the version read; FHIR's mechanism is `If-None-Exist`, not built |
| L5 | The OpenAPI document does not declare the header | It would add a parameter to every generated `POST`; a larger change |

## 5. Tests

| Layer | What it pins |
|---|---|
| Integration, `IdempotencyTests` (20; real MariaDB, the real HTTP pipeline) | A repeat is answered with the first answer and registers, audits and emits once; a different request, route or user is not a repeat (422, 422, own answer); no key stores nothing; malformed and doubled keys are 400; a refused request releases its key; eight simultaneous requests register one patient; a running request is 409 with `Retry-After`; a stale claim is taken over; an answer past the window is not replayed; a retried policy is one policy; a key on the upload is refused; a key on a read is ignored; no sign-in stores nothing; the table holds no request text |
| Unit, `IdempotencyKeyHandlerTests` (WebApp, +2) | The upload route carries no key even while an attempt is open; the real generated Patient Management client sends the same key when a failed registration is retried |
| Mutation | With the upload refusal removed, with the release of a failed request's key removed, and with the WebApp's upload exclusion removed, the right test fails each time |
