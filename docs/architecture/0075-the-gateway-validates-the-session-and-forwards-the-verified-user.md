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
6. **Patient Management is protected by the sign-in requirement but does not yet consume the identity:** its caller-named `RegisteredBy`, `OwnerId` and `AuthenticatorUserId` stay as they are until it has its own uuid-to-id lookup and grant. Stated as a follow-up, not a gap closed.
7. **External FHIR clients:** `/fhir` is under Patient Management's routes, so it now needs a session. A bearer-token path for external clients (legacy's FHIR uses OAuth) is a separate decision.

## Consequences

- Every clinical write in the audit trail names a real user. `unknown` and `user:{caller-named}` actors disappear from Clinical Care.
- Clinical Care's tests need an identity: the test client sends a seeded user's uuid, and a few tests assert the new behaviour (401 without it, 403 for an unknown user, the body `providerId` being ignored).
- A WebApp session that expires mid-use now gets 401 from clinical calls and must sign in again; the pages already treat 401 as signed out where they check, and the panels show their generic error.
- CSRF is unchanged: the cookie is same-origin (ADR 0033) and this ADR adds no new cross-site exposure, but nothing here adds a CSRF token either.
- The check adds one local call per request. If it matters, a short cache keyed by a hash of the cookie is the next step, trading a bounded revocation delay for latency.
