# Changelog 2026-11-02: silent token refresh and admin-initiated session revocation

## Summary

Two gaps from M1's remaining list, closed together: ADR 0029 deferred silent refresh on an expired access
token, and ADR 0028/0040 both named "force-ending a session from outside the browser that holds it (an
admin action, or a stolen-cookie response)" — ADR 0044 closed the stolen-cookie half, leaving the
admin-action half open. See ADR 0045 for the full writeup.

## Added

### Silent refresh

- **`AccessTokenRefresher`** reads the session's `expires_at` on every BFF proxy call and, if the access
  token is expired or within a 30-second skew of expiring, redeems the stored `refresh_token` via
  `OpenIddictClientService.AuthenticateWithRefreshTokenAsync`. The refreshed ticket is persisted back into
  the *same* session row — confirmed empirically (a standalone probe against the real
  `Microsoft.AspNetCore.Authentication.Cookies` implementation) that re-signing-in on an already-
  authenticated request calls the configured `ITicketStore`'s `RenewAsync` with the existing key, not
  `StoreAsync` with a new one, so this adds no cookie churn and no orphaned session rows.
- A failed refresh falls back to the stale access token, the same graceful-degradation path a session with
  no token at all already takes — OpenEMR answers 401 itself. Handles both a rejected refresh grant
  (`OpenIddictExceptions.ProtocolException`) and an open circuit (`BrokenCircuitException`, ADR 0041 — the
  refresh call shares the same token-endpoint breaker as the original code exchange).
- `GrantTypes.RefreshToken` added to the client's static `OpenIddictConfiguration`, and
  `AllowRefreshTokenFlow()` added to the client registration — without both, OpenIddict.Client refuses the
  refresh request outright, since discovery is bypassed and this static configuration is the only thing it
  has to consult.
- `/logout` needed no changes: it already reads whatever `id_token` is on the session's current
  properties, and a refresh that reissues one updates those same properties in place.

### Admin-initiated session revocation

- **`AdminOptions.Subjects`**, bound from a new `Admin` configuration section — a config-driven allowlist
  of OpenEMR subjects trusted to revoke anyone's session. Empty by default, so it fails closed. Not wired
  through `.env`: a list needs indexed environment keys that do not fit the single-scalar convention every
  other setting there uses, and this is a rarely-exercised local-dev feature.
- **`POST /sessions/revoke/{subject}`** deletes every session row for the named subject if the caller's
  own subject is on the allowlist, else 403. If the admin targets their own subject, their own current
  cookie is also signed out, the same immediate-effect reasoning `/sessions/revoke-all` already uses.
- A new `AuditEvents.AdminRevokedSessions(adminSubject, targetSubject)` entry, since this acts on someone
  other than the caller — the one thing `/sessions/revoke-all`'s self-service scope never needed to record.

## Fixed (test bugs, not production code)

- A first draft of the refresh tests set the fake server's `expires_in` to a *negative* value to simulate
  an already-expired token, which OpenIddict.Client itself rejects during the original authorization-code
  exchange (it validates the token response, including `expires_in`), failing the whole login rather than
  exercising the refresh path. Fixed by relying on `AccessTokenRefresher`'s own 30-second skew window
  instead: a short-but-valid `expires_in` (10s) is already "near expiry" immediately after login, no
  negative value or `Task.Delay` needed.
- The same draft read the fake `/ping` endpoint's JSON response with `GetProperty("Authorization")`;
  ASP.NET Core's default minimal-API JSON serialization camel-cases property names, so the real key is
  `"authorization"` — the exact thing `OpenEmrBffProxyEndpointTests` already knew and this new file didn't
  check against first.

## Tests

- Unit tests: 80 pass (up from 71). New: 4 for silent refresh (a near-expiry token is refreshed before the
  proxied call; the refreshed token is reused, not re-refreshed, on a second call; a still-valid token is
  never touched; a rejected refresh falls back to the stale token without failing the request), 4 for
  admin-initiated revocation (an allowlisted admin can revoke another subject's sessions while their own
  stays intact; an admin revoking their own subject is also signed out; a non-admin gets 403 and changes
  nothing; the endpoint requires authentication), and 1 confirming `/logout` still sends a usable
  `id_token_hint` after a refresh occurred.
- Architecture tests: 1 pass, unaffected.
