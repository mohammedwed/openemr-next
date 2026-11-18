# 0077. Creating requests accept an Idempotency-Key, and a SOAP note guards its own retry

- **Status:** Accepted
- **Date:** 2026-11-17
- **Builds on:** [0069](0069-clinical-care-audits-writes-to-its-own-append-only-table.md), [0073](0073-soap-notes-are-addressed-by-their-form-id-within-an-encounter.md), [0075](0075-the-gateway-validates-the-session-and-forwards-the-verified-user.md)
- **Closes:** finding F-CC-8 (create is not idempotent), API-5 of the playbook for Clinical Care, and limitation L2 of the SOAP slice (a retried transaction could write a second note).

## Context

Two different things were called "no idempotency", and they need different fixes.

1. **A client retries.** A browser or a script sends a create, the answer is lost (timeout, dropped connection), and it sends the same create again. Nothing in the service recognised the second request as the first, so a second encounter, a second set of vitals, a second note was written.
2. **The service retries itself.** Every registry runs its write inside an EF Core execution strategy that re-runs the transaction after a transient fault. If the fault arrives at the instant of the commit reply, after the commit has happened, the second run writes again. Most registries survive this because the record has an identity (a uuid) before it is inserted, so the second run finds the first. A SOAP note has no uuid (ADR 0073) and so had no identity to find.

## Decision

1. **A creating request may carry an `Idempotency-Key` header** (1 to 64 letters, digits and `_ . : -`; a UUID works). It applies to every `POST` under `/api/v1`. Without the header nothing changes, so no existing caller is affected.
2. **The key belongs to the verified user** (ADR 0075): the store's key is (user id, key). One user's key cannot replay another's answer, and the check runs after the identity check, so a request that is not signed in never reaches it.
3. **What "the same request" means** is a hash of the method, the path with its query, and the body bytes. Only the hash is stored, never the body.
4. **What a repeat gets:**
   - the same key and the same request after the first one **finished**: the first answer again (status, location, body), marked `Idempotent-Replayed: true`, with nothing created;
   - the same key and the same request while the first is **still running**: **409** with `Retry-After: 5`;
   - the same key with a **different** request: **422**;
   - a malformed key, or two keys: **400**; a body over 1 MB: **413**.
5. **Only success is kept.** A refused or failed request (any non-2xx, or an exception) releases its key, so a corrected retry can reuse it. An answer larger than 64 KB is passed through and its key released: it is never truncated.
6. **Atomic by the primary key, not by read-then-write.** A request inserts its claim with a fresh token and reads the row back; it owns the key only if the token is its own. Of any number of simultaneous requests with one key, exactly one runs.
7. **A claim that is never completed expires.** A request that holds a key for more than five minutes without finishing is presumed dead and a retry may take the key over. The one duplicate this can allow is a request that died between committing its record and saving its answer. That window is the price of not holding a lock across a request; it is stated, not hidden.
8. **The answers are kept 24 hours** in `clinical_care_idempotency`, this service's own table, swept as a side effect of claiming (no job). The service holds full privileges on it, unlike the audit table, because claims are completed, released and deleted.
9. **A SOAP note guards its own retry.** The registry gives each attempt a fresh `AttemptId`, writes it into the `SoapNoteRecorded` event in the same transaction as the note, and, **on a re-run only**, first looks for an event carrying that id; if found it returns that note instead of writing another. The lookup never runs on a first attempt, so the normal path pays nothing. The event gains an additive field, `attemptId`; no consumer reads the payload's other fields differently.
10. **Not done:** the WebApp does not send keys yet, and the OpenAPI document does not declare the header (declaring it changes every generated client signature). Both are follow-ups; the header works today for any caller that sends it.

## Consequences

- A retried create is safe for any caller that sends a key, and a SOAP note is safe against the service's own retry whether or not one is sent.
- A table of keys the service owns, with a 24-hour life, and one extra round trip to the store for requests that carry a key.
- A key that outlives its window is as new as any other.
- A conflict (409) is possible for a caller that retries faster than the first request finishes; the answer says to wait and repeat.
