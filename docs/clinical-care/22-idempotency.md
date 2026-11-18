# Clinical Care slice: idempotency keys and the SOAP retry guard (M5h)

Decisions are in ADR 0077; this is the working record.

## 1. What was built

| Piece | Where |
|---|---|
| Rules (header, key shape, window, lease, fingerprint) | `Application.Ports/Idempotency.cs` |
| Port `IIdempotencyStore`, `ClaimOutcome`, `StoredResponse` | `Application.Ports/IIdempotencyStore.cs` |
| Adapter over the service's own table | `Infrastructure/EfIdempotencyStore.cs` |
| Middleware: claim, run, store or release, replay | `Api/Authentication/IdempotencyMiddleware.cs`, slotted after `UseVerifiedUser` |
| Table `clinical_care_idempotency` and its grant | `scripts/seeds/clinical-care-db.sh` |
| SOAP retry guard: `AttemptId` on the event, lookup on a re-run | `EfSoapNoteRegistry`, `SoapNoteRecordedEvent` |

## 2. Decisions

| # | Decision | Why |
|---|---|---|
| ID-1 | The header is optional and applies to every `POST` under `/api/v1`. | No existing caller changes; one rule for every create. |
| ID-2 | The key is scoped to the verified user. | Never another user's answer. |
| ID-3 | The fingerprint is method + path with query + body bytes, hashed. | A key stands for one request; the table holds no request text. |
| ID-4 | Replay answers with the stored status, location and body and `Idempotent-Replayed: true`; in-progress is 409 with `Retry-After`; a different request is 422. | The standard contract. |
| ID-5 | Only 2xx is stored; anything else releases the key. | A corrected retry must not be locked out by its own refusal. |
| ID-6 | Claiming is atomic through the primary key and a token read back. | No race, no lock across the request. |
| ID-7 | A claim older than five minutes is taken over; answers live 24 hours. | A dead request must not block a retry for ever. |
| ID-8 | The SOAP guard looks for the attempt only on a re-run. | The common path pays nothing. |
| ID-9 | `claim_token` is `VARCHAR(36)`, not `CHAR(36)`. | MySqlConnector reads `CHAR(36)` as a `Guid`; the first run of the tests caught the 500. |

## 3. Limitations

| # | Limitation | Why accepted |
|---|---|---|
| L1 | A request that dies between committing its record and saving its answer, then is retried after five minutes, can create a second record. | The price of no lock across a request. Narrow. |
| L2 | The WebApp does not send keys; the OpenAPI document does not declare the header. | Declaring it changes every generated client signature; a follow-up. |
| L3 | An answer over 64 KB is not protected. | Creating requests answer with identifiers. |
| L4 | Other services (Patient Management) still have no idempotency. | Their own slice (F-2 in their gaps list). |
| L5 | The SOAP guard covers the service's own retry; a client retry is covered by the key. | Two different retries, two mechanisms. |

## 4. Tests

| Layer | What it pins |
|---|---|
| Integration, `IdempotencyTests` (against the real table, as the production principal) | A repeat is answered with the first answer, marked replayed, and creates, audits and emits once; a different request is 422; the same key and body to another route is 422; one user's key never replays another's; no key means no protection and nothing stored; malformed keys (space, semicolon, quote, 65 characters) and two keys are 400; **a refused request releases its key** and a corrected retry succeeds; **eight simultaneous requests create one record** and every answer is the same record; a fresh claim is 409 with `Retry-After`; a claim past the lease is taken over; an answer past the window is not replayed; a retried SOAP note and retried vitals are one record each; a key on a read is ignored; no key gets a caller past sign-in; the table holds no request text |
| Integration, `SoapNoteRetryTests` | **A commit whose acknowledgement is lost, then retried, writes one note** (a real interceptor throws after the commit and a strategy retries on it); two lost acknowledgements in a row still write one; two separate notes are never mistaken for a retry; the event carries `noteId` and `attemptId` |
| Unit, `IdempotencyTests` | Key shape; the fingerprint is stable, case-insensitive on the method, and changes with method, path, query and body; the path/body boundary cannot be shifted |
| Mutation | With the claim disabled and the SOAP guard disabled, 11 tests fail (including a real duplicate note); both restored |
