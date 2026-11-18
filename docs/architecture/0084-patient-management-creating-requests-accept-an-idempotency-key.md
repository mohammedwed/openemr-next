# 0084. Patient Management's creating requests accept an Idempotency-Key

- **Status:** Accepted
- **Date:** 2026-11-18
- **Builds on:** [0077](0077-creating-requests-accept-an-idempotency-key-and-soap-notes-guard-their-own-retries.md) (the same design, for Clinical Care), [0075](0075-the-gateway-validates-the-session-and-forwards-the-verified-user.md) (the verified user the key is scoped to), [0078](0078-patient-management-keeps-its-own-append-only-audit-trail.md)
- **Closes:** finding F-2 of `docs/patient-management/05-workflows-and-events.md` (no idempotency: a double-clicked Register or Add policy creates a second record) and P1-6 of `07-gaps-and-evolution.md` for the creating routes

## Context

Patient Management had no protection against a repeated request. A double-clicked button, or a client retrying after a lost reply, registered a second patient or added a second policy. Clinical Care solved this in M5h (ADR 0077). The behaviour is meant to be the same in both services, so this is a port, not a redesign.

## Decision

1. **A creating `POST` under `/api/v1` may carry an `Idempotency-Key`** (1 to 64 letters, digits and `_ . : -`). It applies to registering a patient, adding an insurance policy and swapping insurance. Without the header nothing changes.
2. **Everything else in ADR 0077's design is the same:** the key belongs to the verified user; "the same request" is a hash of method, path with query, and body bytes (only the hash is stored); a repeat after the first one finished gets the first answer again (`Idempotent-Replayed: true`) and creates nothing; while the first still runs, 409 with `Retry-After: 5`; the same key with a different request, 422; a malformed key or two keys, 400; a body over 1 MB, 413. Only a 2xx answer is kept, so a refused or failed request releases its key. Claiming is atomic by the table's primary key; a claim not completed in five minutes may be taken over; answers are kept 24 hours in `patient_management_idempotency` (this service's own table, swept as a side effect of claiming). The code is a port of Clinical Care's, with the names changed.
3. **A document upload takes no key, and a key sent there is refused with 400.** The advance-directive document upload carries its file as base64 in a JSON body of up to about 30 MB, far over the 1 MB a request can be fingerprinted at. Quietly ignoring a key there would leave a caller believing a retry was safe, so it is refused.
4. **The WebApp sends keys** from the Register, Add policy and Swap insurance dialogs, with the same attempt mechanism as Clinical Care's dialogs (ADR 0077 #10): one attempt per user save, kept while the save is retried, ended by a success. The handler is registered on the Patient Management client too, and **skips the upload route**, so an attempt that outlived a failed dialog can never tag an upload.
5. **Not covered, on purpose:** `PUT` (the demographics correction and the directive status are already conditional on the version read), `POST /fhir/Patient` (FHIR's own mechanism is `If-None-Exist`, which is not built; this middleware applies under `/api/v1` only), and the document upload (point 3).

## What this does not fix

- **F-1 is still open.** A registration that commits and *then* fails (scoring runs after the commit, in the endpoint) answers 500, and a 500 releases its key, so a retry registers a second copy. A key protects against a repeated request whose first attempt *answered*; closing F-1 needs scoring moved out of the request.
- **The window between commit and saving the answer.** The key is claimed and completed in its own statements, not in the change's transaction (the Patient Management workflow note described it as the same transaction). A request that dies in the instant between committing the patient and recording its answer leaves a claim that expires after the lease, and a retry then can create a second patient. Clinical Care accepts the same window and says so (ADR 0077 #7).
- **A transaction re-run by the service's own retry strategy** (a lost commit acknowledgement) is not guarded the way Clinical Care guards a SOAP note. Registration's registry has no `AttemptId` equivalent yet.
- **The OpenAPI documents of both services do not declare the header.** Declaring it would add a parameter to every generated `POST` in the WebApp's clients; that is a larger change than this one.

## Consequences

- A retried or double-submitted registration, policy or swap from the WebApp is one record.
- A table of keys Patient Management owns, with a 24-hour life, and one extra round trip to it for requests that carry a key.
- The seed script creates the table and grants the service full privileges on it; the least-privilege user cannot reach anything else through it.
- Twenty integration tests pin the behaviour against a real MariaDB (the same list as Clinical Care's suite, applied to registration and policies, plus the upload refusal); two WebApp tests pin the handler. Removing the upload refusal, or the release of a failed key, or the WebApp's upload exclusion, each fails the right test.
