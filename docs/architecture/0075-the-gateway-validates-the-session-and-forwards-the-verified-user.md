# 0075. The gateway validates the session and forwards the verified user to Clinical Care and Patient Management

- **Status:** Accepted
- **Date:** 2026-11-17
- **Supersedes in part:** [0048](0048-authfacade-stays-the-bridge-gateway-validation-deferred.md) (gateway validation deferred) and [0055](0055-patientmanagement-api-gated-by-a-shared-secret-identity-forwarding-deferred.md) (identity forwarding deferred), for the two services named here.
- **Builds on:** [0027](0027-persist-tokens-in-the-session-cookie.md), [0033](0033-gateway-fronts-webapp-and-authfacade.md), [0064](0064-clinical-care-shares-the-legacy-database-and-coexists-with-legacy-encounter-writers.md), [0069](0069-clinical-care-audits-writes-to-its-own-append-only-table.md)
- **Closes:** the critical finding that the Clinical Care and patient APIs were reachable through the gateway with no sign-in; finding F-CC-1 (the acting provider was whatever the caller named).

## Context

The gateway attached each backend's shared secret to **every** request for its routes, whether or not the caller was signed in, so the secret proved only "this came through the gateway". Anyone who could reach the gateway could read and write clinical data. The services trusted a caller-named `providerId` for who was acting, and the audit trail recorded that as an **unverified** actor, or `unknown` for updates.
ADR 0055 deferred forwarding the real identity for two reasons: the session carries only `sub`, which is OpenEMR's user **uuid** (`users.uuid`), not the numeric `users.id` that legacy's columns hold; and there was no lookup between them. Clinical Care already reads `users` (granted columns `id`, `username`), so for that service the lookup is one more granted column, not an open question.
AuthFacade keeps sessions in a server-side store (ADR 0043, 0044) so they can be revoked, and exposes `GET /api/v1/me`, which answers 200 with the session's `sub` and `name` for a valid cookie and 401 otherwise.

## Decision

1. **The gateway asks AuthFacade whether the request has a valid session** (`GET /api/v1/me`, forwarding only the caller's `Cookie`, with the original scheme and address) for every request to a Clinical Care or Patient Management route. No cookie, or a refused one, is **401** at the gateway and the request is never forwarded. AuthFacade unreachable or answering anything unexpected is **503**: **fail closed**. This reuses AuthFacade's own revocable session store, so a revoked session stops working on the next request.
2. **The gateway forwards the verified user** to those two clusters as `X-Authenticated-User: <OpenEMR user uuid>`, and **deletes any caller-supplied value of that header on every route**, so it can only ever come from the check. It is trusted by the service for the same reason the secret is: the service accepts only the gateway (ADR 0055).
3. **No caching** of the check. Revocation takes effect immediately, at the cost of one internal call per request. This is a deliberate first position, to be revisited with a measurement.
4. **Clinical Care requires the identity on every `/api/v1` route** (the internal event endpoint is service-to-service and is exempt), as **middleware that runs before any request body is bound**, so a caller with no identity and a bad body is told to sign in (401), not about the body (a test caught the first version, an endpoint filter, answering 400): a missing header is 401. It resolves the uuid to `users.id` and `username` with its own read (`users.uuid` is added to its column grant); a uuid that matches no user is **403**, not a silent anonymous write.
5. **The verified user is the acting provider and the audit actor** (`Verified = true`, subject = the uuid, user id = `users.id`). A `providerId` in a request body is **ignored**: it can no longer name who is acting. It stays on the contracts, optional, so existing callers do not break. The encounter's own provider (a field of the visit, not the actor) defaults to the acting user when omitted.
6. **Patient Management applies the same rule** (added the same day): the same middleware on `/api/v1` and `/fhir`, resolving the uuid with its own read of `users` (`id`, `uuid` and, new, `active`; still no credential columns). The verified user is who **registers a patient** (`created_by`, also for FHIR create), **owns an uploaded document** and **reviews an advance directive**; the `RegisteredBy`, `OwnerId` and `AuthenticatorUserId` a body names, and the FHIR `?registeredBy=`, are ignored and optional. Patient Management has no audit trail of its own, so this changes who legacy's columns name, not an audit table.
7. **External FHIR clients:** `/fhir` is under Patient Management's routes, so it now needs a session, and an external system with no browser session cannot call it. **A bearer-token path for them is deliberately not built:** nothing in this repository calls `/fhir` from outside, and the right design (validate OpenEMR-issued OAuth tokens by introspection, with its own client credentials and scopes) depends on which clients exist. It needs its own ADR once there is one.

## Consequences

- Every clinical write in the audit trail names a real user. `unknown` and `user:{caller-named}` actors disappear from Clinical Care.
- Clinical Care's tests need an identity: the test client sends a seeded user's uuid, and a few tests assert the new behaviour (401 without it, 403 for an unknown user, the body `providerId` being ignored).
- A WebApp session that expires mid-use now gets 401 from clinical calls and must sign in again; the pages already treat 401 as signed out where they check, and the panels show their generic error.
- CSRF is unchanged: the cookie is same-origin (ADR 0033) and this ADR adds no new cross-site exposure, but nothing here adds a CSRF token either.
- The check adds one local call per request. If it matters, a short cache keyed by a hash of the cookie is the next step, trading a bounded revocation delay for latency.

## Amendment, Nov 17 (M5f): the `users` grant is seven columns, not four

Clause 4 above describes Clinical Care's grant on `users` as the columns that resolve the verified uuid (`id`, `username`, `uuid`, `active`). The providers lookup (`docs/clinical-care/18-lookups-slice.md` section 5) added `fname`, `lname` and `authorized`, so that a caller can choose a provider by name from the people legacy marks as providers. The user chose this over a username-only list. The credential and contact columns remain unreadable, and the grant tests pin the exact set of seven. Nothing else in this decision changes.

## Amendment, Nov 17 (M5j): what the per-request check costs, read from the code

The decision above calls for measuring before caching. A measurement needs a signed-in session cookie against the running stack, which was not available, so this is **an analysis of what the check does, not a benchmark**. A benchmark is still owed.

- **One check is:** one internal HTTP call from the gateway to AuthFacade, which reads the session row by primary key (`AuthFacadeSessionStore.RetrieveAsync` does `Sessions.FindAsync(key)` in a fresh scope, so a database point read each time), unprotects the stored ticket (Data Protection) and deserializes it. The cookie has `SlidingExpiration = false` (12 hours), so a check **never writes**.
- **How often:** once per request to a Clinical Care or Patient Management route. An encounter page loads about half a dozen panels, so about half a dozen checks per page view, in parallel.
- **Reading:** a primary-key read and a small decrypt per request, over the internal network, is a few milliseconds at most and scales with the number of users, not the number of records. At the size of one clinic it is not a hot spot, and the revocation guarantee (a revoked session stops on the next request) is worth more than the milliseconds. **Do not add the cache yet.** If a benchmark shows a problem, the cheapest step is a short in-memory cache in the gateway keyed by a hash of the cookie, with a TTL of a few seconds, which bounds how long a revoked session keeps working.
- **What to measure when there is a session:** p50 and p95 of an authenticated gateway request against the same request direct to the service, at one and at twenty concurrent users, and the number of `/me` calls per encounter page.
- **A separate finding from the same read:** expired session rows are deleted only when that same session is read again (`RetrieveAsync` removes an expired row it finds). A session that is abandoned and never read again is **never deleted**, so `Sessions` grows without bound over time. **Fixed (M5l):** creating a new session now removes up to 100 expired ones (the keys are read, then deleted by key, so it works on SQLite and MariaDB alike). No background job is needed and the table grows only when sessions are created; a backlog is worked off over the next sign-ins. Sessions with no expiry are never swept.
