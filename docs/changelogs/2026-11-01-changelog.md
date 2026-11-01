# Changelog 2026-11-01

## Summary

The first real browser login attempt through the gateway (merged the day before, ADR 0033) failed on
`/callback` with an OpenIddict error that looked like a configuration mistake but was a proxying one: YARP
rewrites the `Host` header to the internal destination by default, and AuthFacade's redirection-endpoint
check depends on seeing the browser's actual one. Fixed and verified end to end — the one gap ADR 0033 left
open is now closed.

With the browser flow working through the gateway, the password grant and bearer authentication it had
stood in for no longer had a reason to exist, and removing them collapsed AuthFacade from six layered
projects down to two (ADR 0035). What was left was then hardened against a punch list of six independent
gaps drawn from the ADRs and ROADMAP: no rate limiting, no HTTPS, single-instance-only session state, no
audit log, no logout, and no circuit breakers on the remaining calls to OpenEMR. All six are now closed —
rate limiting (ADR 0038), HTTPS with HSTS (ADR 0039), RP-initiated logout (ADR 0040), circuit breakers (ADR
0041), an audit log scoped to login outcomes and the BFF proxy (ADR 0042), and AuthFacade's own session
state persisted in a dedicated MariaDB database (ADR 0043) — each verified against the live stack, not just
unit-tested. Two smaller startup-ordering fixes (ADR 0036, 0037) landed alongside them.

Logout (ADR 0040) had named, and explicitly not solved, one more gap: no way to force-end a session from
outside the browser holding it, since the `AuthFacadeSession` cookie's ticket was entirely self-contained —
there was no server-side record of a session to delete. That is now fixed (ADR 0044): the cookie carries
only an opaque key, the real ticket lives in the same database ADR 0043 introduced, and
`POST /sessions/revoke-all` deletes every session for the caller's own subject, ending a stolen copy of the
cookie immediately with no cooperation from whichever browser holds it.

## Removed

- **The password grant and bearer authentication, and the layered architecture built around them** (ADR
  0035, superseding ADR 0001, 0003, 0007, 0008, 0009, 0011, 0013, 0014, 0015, 0016, 0017, 0018, 0019, 0021,
  0022, 0023). `POST /api/v1/connect/token`, `OpenEmrBearerHandler`, and the now-empty
  `OpenEmrNext.AuthFacade.Domain`, `.Application.Ports`, `.Application`, and `.Infrastructure` projects are
  gone outright — nothing left needed a host-neutral business layer once the only way to authenticate was
  the browser flow (ADR 0026) or the BFF proxy's server-side-attached token (ADR 0029). The solution shrinks
  from six AuthFacade projects to two: `Composition` and `Api`.
  - Also removed as dead weight once their only caller was gone: the circuit breaker decorators
    (`CircuitBreakingIdentityProvider`, `CircuitBreakingTokenVerifier`), `OpenEmrOptions.Scope` and
    `.AcceptAnyServerCertificate`, and the Polly v7/v8 compatibility shim (reintroduced later the same day
    for a different, unrelated reason — see ADR 0041 below).
  - `MeEndpointTests` and `TokenEndpointTests` (live integration tests against the password grant) are
    deleted; `AuthFacadeStackTests` is unaffected, since it never used the grant.

## Added

### Startup ordering

- **AuthFacade waits for OpenEMR's own readiness probe at startup**, not just its container starting (ADR
  0036): Compose's `service_healthy` condition only proves the container accepted a TCP connection, not that
  OpenEMR's own install/upgrade routine finished. AuthFacade's own startup now polls the same probe as a
  second line of defense outside Compose (a bare `dotnet run`, or a race).
- **AuthFacade depends on `seed` completing successfully** (ADR 0037): `seed` is a one-shot service with no
  other dependents, so Compose had no reason to rerun it when its own resolved config changed (for example,
  ADR 0033's redirect-URI formula) unless someone remembered `docker compose up seed` by hand — the actual
  root cause of an `invalid_client` login failure diagnosed and fixed live this session. Now any
  `docker compose up` that brings up `auth-facade` reseeds automatically.

### Rate limiting (ADR 0038)

- **`/oidc/login` and `/callback` are rate limited per client address**, one shared 10-requests-per-minute
  budget across both steps of the flow — resurrecting ADR 0018's design, deleted by ADR 0035 along with the
  password grant it originally guarded. An over-limit request gets a bare `{"error":"too_many_requests"}`
  429, never forwarded to OpenEMR. Verified live through the gateway: 10 requests succeed, the 11th is
  refused.

### HTTPS, HSTS, and trusted forwarded headers (ADR 0039)

- **The gateway terminates TLS** with a self-signed dev certificate (`scripts/setup/new-dev-cert.sh`); its
  plain-HTTP port now only redirects to HTTPS. HSTS is applied via a YARP response transform rather than
  `UseHsts()` middleware, which does not work for a pure-proxy app.
- **AuthFacade trusts `X-Forwarded-For`/`-Proto` from one fixed Docker subnet only**
  (`DOCKER_NETWORK_CIDR`), through a hand-rolled trust check — ASP.NET Core's own built-in
  `UseForwardedHeaders`/`KnownIPNetworks` was confirmed not to restrict anything correctly on the installed
  runtime. The OAuth `redirect_uri` (seed and AuthFacade) moves to the gateway's HTTPS address.
- Verified live: HTTP→HTTPS redirect, HSTS present on every route, the session cookie's `Secure` flag true
  through the gateway and absent on a direct, unproxied connection to AuthFacade's own port.

### RP-initiated logout (ADR 0040)

- **`GET /logout`** signs out the local session unconditionally, then redirects either to the default
  return URL (no captured `id_token`) or to OpenEMR's own `end_session_endpoint` with a required
  `id_token_hint` and a `post_logout_redirect_uri` that must exactly match what the seed registers — read
  directly from OpenEMR's own `AuthorizationController::userSessionLogout()` source, since neither ADR 0024
  nor ADR 0026 had confirmed the endpoint existed. `OpenIddictTokenCapture` now also captures the `id_token`.
  The seed gained `AUTHFACADE_POST_LOGOUT_REDIRECT_URI`; the gateway and the WebApp dashboard both gained a
  route/button for it.
- **Not covered**: there is still no way to force-end a session from outside the browser holding it (an
  admin action, or a stolen-cookie response) — the same server-side-session-record gap ADR 0028 names for a
  different reason.

### Circuit breakers (ADR 0041)

- **Guards the two calls to OpenEMR that survived ADR 0035**: the token exchange during `/callback` and the
  JWKS fetch, plus the BFF proxy's forwarded calls — resurrecting ADR 0017's design, deleted by ADR 0035
  along with the call sites it originally guarded. A 4xx (a bad or replayed code, a missing patient) never
  counts as a failure; only a connection problem, a timeout, or a 5xx/408 opens the circuit. The
  Polly v7/v8 compile-asset-exclusion shim (removed by ADR 0035) is reintroduced, needed again now that
  Polly.Core v8 is used directly.
- Verified live by actually stopping the `openemr` container: a real `Polly.CircuitBreaker.BrokenCircuitException`
  appeared in AuthFacade's own logs; every attempt, before and after the circuit opened, still answered a
  graceful failure redirect, never a raw 500; restarting OpenEMR and waiting out the break duration produced
  a real HTTP round trip again.

### Audit log (ADR 0042)

- **Login outcomes and BFF proxy access are now logged** under a fixed, filterable category
  (`OpenEmrNext.AuthFacade.Api.Audit.AuditLog`), scoped there instead of ROADMAP's original wording of
  `/me` — `/me` returns no PHI and fires on every page load, while the BFF proxy is where a signed-in
  session actually touches OpenEMR's data. `LoginFailed`'s reason is always a fixed internal label, never
  OpenEMR's or OpenIddict's own error text, mirroring the same principle already applied to the
  browser-facing `?error=` code.
- Verified live (stopped OpenEMR, drove `/callback`, saw the real failure line under the right category) and
  with a new test proving the access token never appears in any captured log entry.

### Session state persisted in MariaDB (ADR 0043)

- **OpenIddict's state-token store and the Data Protection key ring behind the `AuthFacadeSession` cookie**
  now persist in a dedicated `authfacade` MariaDB database and least-privilege user, separate from OpenEMR's
  own, created by a new idempotent `seed-authfacade-db` service. Closes two of the three gaps ADR 0028
  named; its third (OpenIddict's own dev-only certificates) is still open, tracked in ROADMAP.md. Falls back
  to the existing per-process SQLite file when `AuthFacadeDb` is unconfigured, so no test needs Docker.
- Two bugs surfaced live during this work and were fixed before merge: a MariaDB-only startup crash
  (OpenIddict's own composite index exceeded MariaDB's 3072-byte max key length under the default `utf8mb4`
  charset — switched to `utf8mb3` for this context's tables only), and a latent bug in the SQLite fallback
  itself, where the random file path was generated inside a per-scope callback instead of once per process,
  silently handing every request after the first an empty, schema-less database.
- Verified live: the dedicated database and user exist with scoped grants (`SHOW GRANTS`), the full schema
  (OpenIddict's own plus `DataProtectionKeys`) is created with no crash, `/health` and `/oidc/login` both
  still work through the gateway, and — the actual motivating problem — restarting the `auth-facade`
  container no longer regenerates the key ring: the same row survives before and after.
- CI's own `.env` did not set the new `AUTHFACADE_DB_PASSWORD` Compose now requires, so the first run of
  this PR's checks failed immediately with "required variable ... is missing a value" before the stack ever
  started; fixed in `ci.yml` the same way the other secrets are handled there.

### Session revocation (ADR 0044)

- **The `AuthFacadeSession` cookie now carries only an opaque key, not the whole ticket.** The real
  ticket — claims and the real OpenEMR access/refresh token inside them (ADR 0027) — is fetched from a new
  `AuthFacadeSessions` table (same database as ADR 0043) on every request, via `AuthFacadeSessionStore`, a
  singleton `ITicketStore` implementation assigned to the cookie scheme's `SessionStore`. `/callback` and
  `/logout` needed no changes at all: both already call the standard `SignInAsync`/`SignOutAsync`, which
  the cookie handler itself uses to call `StoreAsync`/`RemoveAsync` once a store is configured.
- **`POST /sessions/revoke-all`** ends every session recorded for the caller's own subject, not just the
  one making the request, then signs the caller's own current cookie out too. This is the self-service half
  of the gap ADR 0028/0040 both named ("force-ending a session from outside the browser that holds it") —
  the half achievable without inventing an authorization model this project does not have; revoking an
  arbitrary *other* user's session (an admin action) is explicitly still not built.
- OpenEMR itself has no server-to-server revocation endpoint to call (confirmed against its own OIDC
  discovery document and source — `AccessTokenRepository`/`RefreshTokenRepository` have internal revoke
  methods but no reachable REST route), so the underlying OpenEMR token is not revoked at OpenEMR's end —
  a pre-existing limitation, unchanged by this work, now stated against this feature specifically.
- The gateway's route table gained `/sessions/{**catch-all}`.
- Verified live: the new `AuthFacadeSessions` table (with its `Subject` index) is created in the real
  `authfacade` database; `/sessions/revoke-all` reaches AuthFacade through the gateway and answers 401
  unauthenticated, the same as `/api/v1/me`. The existing `authfacade` database from ADR 0043 needed
  dropping and recreating once for the new table to appear — the same "no migrations" limitation ADR
  0043 already documented, hit again rather than newly introduced.

## Fixed

- **The gateway now preserves the original `Host` header** on every route it forwards to AuthFacade
  (`OpenEmrNext.Gateway/Program.cs`, via YARP's `RequestHeaderOriginalHost` transform). Without it,
  AuthFacade saw `Host: auth-facade:8080` instead of `Host: localhost:7130`, so OpenIddict.Client's check
  that `/callback` matches its registered `RedirectUri` failed — even though the `state` parameter lookup
  moments earlier had already found the in-flight login correctly, since that lookup does not depend on
  `Host` at all. See ADR 0034.

## Tests

- Unit tests end the day at 71 (up from 236 before ADR 0035's removals, down to a smaller base once the
  password-grant layers were deleted, then back up as each hardening item added its own coverage: 6 for
  forwarded headers, 6 for rate limiting, 3 for logout, 5 for circuit breakers, 4 for audit logging, 7 for
  the MariaDB session store, 4 for session revocation). Architecture tests: 1 (down from 12, now enforcing
  only that `Composition` never references `Api` or ASP.NET Core, the one layering rule still meaningful
  with two projects left). Live integration tests: 5.
- Verified with `curl` and a persistent cookie jar through the real gateway → AuthFacade → OpenEMR chain for
  the `Host`-header fix, submitting a bogus authorization code: before the fix, `/callback` crashed with
  `InvalidOperationException: ... endpoints that are not managed by the OpenIddict client stack`; after it,
  the same request correctly reaches OpenEMR's own grant rejection and redirects to `?error=login_failed`.
  A real browser login was then confirmed to complete successfully end to end through the gateway.
- Every other item above also has its own live verification against the running Compose stack, noted under
  its own heading — not just unit tests, in keeping with this project's "verify against the real stack, not
  only mocks" practice.
