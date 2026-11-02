# 0045. Silent token refresh, and admin-initiated session revocation

- **Status:** Accepted
- **Date:** 2026-11-02
- **Builds on:** [0027](0027-persist-tokens-in-the-session-cookie.md), [0029](0029-yarp-proxies-browser-sessions-to-openemrs-api.md), [0040](0040-rp-initiated-logout.md), [0041](0041-circuit-breakers-guard-the-remaining-openemr-calls.md), [0044](0044-session-store-backs-the-cookie-for-revocation.md)

## Context

Two gaps were left open on purpose by earlier work:

1. **No silent refresh.** ADR 0029 named it directly: "silent refresh on an expired access token is
   explicitly deferred rather than forgotten." Once the session's access token expires, every BFF proxy
   call fails at OpenEMR with no recovery — the browser has no way to know its session is still otherwise
   good, only that OpenEMR started saying no.
2. **No admin-initiated session revocation.** ADR 0028/0040 both named "force-ending a session from
   outside the browser that holds it (an admin action, or a stolen-cookie response)." ADR 0044 closed the
   stolen-cookie half (self-service `/sessions/revoke-all`) but left the admin-action half explicitly open:
   AuthFacade has no authorization model to decide who counts as an admin.

Both are now addressed.

## Decision

### Silent refresh

1. **`AccessTokenRefresher`** reads the session's `expires_at` on every BFF proxy call and, if the access
   token is expired or within a 30-second skew of expiring, redeems the stored `refresh_token` via
   `OpenIddictClientService.AuthenticateWithRefreshTokenAsync` — OpenIddict.Client's own lower-level
   service API, not the browser-driven authorization-code flow. Only the BFF proxy needs this; `/me` never
   calls OpenEMR.
2. **The refreshed ticket is persisted back into the same session row**, not a new one: calling
   `SignInAsync` again on an already-authenticated request calls the configured `ITicketStore`'s
   `RenewAsync` with the *existing* key (confirmed empirically — see Consequences), not `StoreAsync` with a
   new one. No new cookie churn, no orphaned rows.
3. **A failed refresh falls back to the stale access token**, the same graceful-degradation path a session
   with no token at all already takes (ADR 0013's passthrough) — OpenEMR answers 401 itself; this proxy
   makes no auth decision of its own. Both a rejected refresh grant (`OpenIddictExceptions.ProtocolException`)
   and an open circuit (`BrokenCircuitException`, ADR 0041 — the refresh call shares the same token-endpoint
   breaker as the original code exchange, since both go through the same OpenIddict.Client HTTP pipeline)
   are handled this way.
4. **`GrantTypes.RefreshToken` added to the client's static `OpenIddictConfiguration`**, alongside
   `AuthorizationCode`, and `AllowRefreshTokenFlow()` added to the client registration — without both,
   OpenIddict.Client refuses the refresh request outright, since discovery is bypassed (ADR 0012/0024) and
   this static configuration is the only thing it has to consult.
5. **`/logout` needed no changes.** It already reads whatever `id_token` is currently on the session's
   properties; a refresh that reissues one updates those same properties in place, so logout always sends
   the latest one. Verified with a test that refreshes, then logs out, and still gets a usable
   `id_token_hint`.

### Admin-initiated revocation

1. **`AdminOptions.Subjects`**, bound from the `Admin` section — a config-driven allowlist of OpenEMR
   subjects trusted to revoke anyone's session. Not a role or claim read from OpenEMR itself: AuthFacade has
   no authorization model to validate such a claim against (the same gap `/api/v1/me` already has), and a
   fixed list matches this project's conservative-local-stack precedent (ADR 0020). Empty by default —
   fails closed, not open.
2. **`POST /sessions/revoke/{subject}`** deletes every session row for the named subject (not the caller's
   own, necessarily) if the caller's own subject is on the allowlist; otherwise 403. If the admin happens to
   target their own subject, their own current cookie is also signed out, the same immediate-effect
   reasoning `/sessions/revoke-all` already uses.
3. **Logged to the audit trail** (extending ADR 0042's `AuditEvents`, not reopening its scope decision):
   who did it and against whom, since this acts on someone other than the caller — the one thing
   `/sessions/revoke-all`'s self-service scope never needed to record.

## Consequences

- The BFF proxy keeps working across an access token's natural expiry without the browser ever noticing,
  closing ADR 0029's deferred gap. Verified against a fake OpenEMR server: a near-expiry token is refreshed
  before the first proxied call reaches it; the refreshed token is reused (not re-refreshed) on a second
  call; a rejected refresh falls back to the stale token without failing the request; logout after a
  refresh still carries a usable `id_token_hint`.
- Confirmed empirically (a standalone probe against the real `Microsoft.AspNetCore.Authentication.Cookies`
  implementation, not assumed from documentation) that re-signing-in on an already-authenticated request
  renews the existing `ITicketStore` key rather than minting a new one — the detail this whole refresh
  design depends on to avoid Set-Cookie churn and orphaned session rows.
- The admin-action half of ADR 0028/0040's named gap is closed, narrowly: an allowlisted subject can end
  any other subject's sessions. This is not a general authorization system — `/api/v1/me`'s own "authorization
  for OpenEMR data is not built" gap is untouched, and nothing here lets an admin do anything to OpenEMR's
  own data, only to AuthFacade's session records.
- Neither feature reaches OpenEMR's own side. OpenEMR has no server-to-server token revocation endpoint
  (ADR 0044 already confirmed this); a refreshed or revoked AuthFacade session has no bearing on whether
  the underlying OpenEMR refresh token itself still works outside AuthFacade.
- `Admin:Subjects` is deliberately not wired into `docker-compose.yml`/`.env.example`: it is a `List<string>`,
  which needs indexed environment keys (`Admin__Subjects__0`, `__1`, …) that do not fit the single-scalar
  `.env` convention every other setting here uses, and this is a rarely-exercised local-dev feature, not
  one every stack needs to configure. An operator who wants to test it sets it directly (environment
  variables, `appsettings.Development.json`, or user-secrets).

## Alternatives considered

- **Trust an OpenEMR role/group claim for admin authorization**, instead of a config allowlist. Rejected
  for now: the current OIDC scope (`openid profile email offline_access`) carries no such claim, and
  widening it is a separate decision with its own consequences (the same reasoning ADR 0029 already applied
  to not widening scope for `api:oemr`).
- **`OnValidatePrincipal`-based refresh**, checking expiry on every authenticated request regardless of
  whether it calls OpenEMR. Rejected: `/me` never calls OpenEMR, so refreshing for it is pure overhead with
  no payoff; scoping the check to the one call site that actually needs a valid token (the BFF proxy's
  request transform) is simpler and does not touch the authentication pipeline itself.
- **Revoking a session also attempting to reach OpenEMR's own session/token.** Rejected: not possible —
  see Context and Consequences.

## Related decisions

Closes the silent-refresh gap [0029](0029-yarp-proxies-browser-sessions-to-openemrs-api.md) named, and the
admin-action half of the gap [0028](0028-browser-login-and-session-assume-a-single-instance.md) and
[0040](0040-rp-initiated-logout.md) both named — [0044](0044-session-store-backs-the-cookie-for-revocation.md)
closed the self-service half. Shares the token-endpoint circuit breaker
[0041](0041-circuit-breakers-guard-the-remaining-openemr-calls.md) already built. Extends
[0042](0042-audit-log-scoped-to-login-and-the-bff-proxy.md)'s audit trail with one new event, without
reopening that ADR's own scope decision.
