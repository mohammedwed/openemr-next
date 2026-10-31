# 0029. YARP proxies a browser session's API calls to OpenEMR, with the real token attached server-side

- **Status:** Accepted; the Consequences question about reaching other backend services is resolved by [0030](0030-bff-proxy-forwards-to-the-gateway-not-each-service.md)
- **Date:** 2026-10-31
- **Builds on:** [0013](0013-pass-openemr-tokens-through.md), [0024](0024-browser-authorization-code-flow-with-pkce.md), [0026](0026-openiddict-client-drives-the-authorization-code-flow.md), [0027](0027-persist-tokens-in-the-session-cookie.md)

## Context

ADR 0027 made the real OpenEMR access/refresh token survive past `/callback`, inside the `AuthFacadeSession`
cookie's own encrypted ticket, but left the actual consumer unbuilt: "nothing reads these tokens yet." A
browser-based client that completed the OIDC login (ADR 0026) holds only that session cookie — by design, it
was never handed a bearer token (ADR 0024's original reasoning: a token in JS-reachable storage is an XSS
target) — so it has no way to call OpenEMR's own API on its own. Closing that gap means AuthFacade acting as a
token-handling reverse proxy: the browser calls AuthFacade, AuthFacade attaches the real token server-side and
forwards to OpenEMR, and OpenEMR's response is relayed back unchanged, the same passthrough principle ADR 0013
already established for the token endpoint.

This combined role — the same service drives the OIDC login *and* proxies API calls with the token attached —
is not a one-off invention here. It is the architecture the IETF's OAuth-for-browser-apps guidance recommends
over putting tokens in the browser at all, and it is exactly what reference implementations (Duende.BFF, built
on ASP.NET Core's OIDC/cookie handlers plus YARP) do: one backend-for-frontend host, not two separate services.

A library choice was needed for the proxying mechanics themselves — request/response forwarding, header
handling, streaming — the same category of "fiddly but well-solved" problem that cost seven rounds of fixes
when PKCE/state/nonce were hand-rolled before OpenIddict.Client replaced them (ADR 0026). Two real options
were weighed:

- **Duende.BFF.** The most complete fit — session management plus a YARP-based proxy with automatic token
  attach/refresh/revoke. Rejected for now: it is commercially licensed above a small-revenue threshold, and it
  brings its own session/token-management stack that would duplicate, not reuse, what ADR 0026/0027 already
  built directly on OpenIddict.Client and ASP.NET Core's cookie handler — adopting it now would mean ripping
  out working code.
- **YARP with a custom token-attaching transform.** Free, MIT-licensed, and already the tool ROADMAP.md's M3
  had picked for the future API gateway — using it here pulls forward a dependency already planned, rather
  than introducing an unplanned one.

A fully hand-rolled proxy (raw `HttpClient` forwarding, no library) was also considered and rejected: request
forwarding has the same hidden complexity (headers, streaming, error mapping) that justified reaching for
OpenIddict.Client instead of continuing to hand-roll the login flow.

## Decision

1. **`Yarp.ReverseProxy`**, added to the Api project only (`OpenEmrNext.AuthFacade.Api.csproj`).
2. **`OpenEmrBffProxyServiceCollectionExtensions.AddOpenEmrBffProxy`** (`Api/Proxy/`) builds one route and one
   cluster in memory (`LoadFromMemory`, not `appsettings.json`-driven config) because the destination —
   `OpenEmr:BaseUrl` — is only known from `IConfiguration` at startup, the same way `OpenIddictClientServiceCollectionExtensions`
   already reads it. The route matches everything under `/bff/openemr/{**catch-all}` and strips that prefix
   before forwarding, so OpenEMR sees the same path it would if called directly.
3. **A code-based request transform** (`AddTransforms`) reads the session's access token with
   `HttpContext.GetTokenAsync(SessionScheme, "access_token")` — the same extension ADR 0027's test proved
   round-trips through the encrypted cookie ticket — and sets it as the proxied request's `Authorization:
   Bearer` header. A session with no token attaches no header at all; OpenEMR then rejects the request on its
   own, since this proxy makes no authorization decision of its own (ADR 0013's passthrough, extended from the
   token endpoint to every proxied call).
4. **`OpenEmrBffProxyEndpoints.MapOpenEmrBffProxyEndpoints`** (`Api/Endpoints/`) maps `app.MapReverseProxy()`
   with its own authorization policy requiring the `AuthFacadeSession` scheme specifically — not the default
   policy's Bearer-or-session either/or that `/me` uses. A caller that already holds a bearer token has no
   need for this proxy and should call OpenEMR directly; letting Bearer satisfy it here would make the proxy
   a redundant, more roundabout path to the same call.
5. **No silent refresh on an expired access token.** If the token the session holds has expired, the proxy
   attaches it anyway and lets OpenEMR's own 401 pass through unchanged — the same deferral ADR 0027 already
   recorded ("the refresh token... outlives the access token... so a later proxy can refresh silently," not yet
   built) and the same open item ROADMAP M1 already tracks ("decide the refresh and revocation approach").

## Consequences

- A browser session can now reach OpenEMR's API through AuthFacade without ever holding a bearer token,
  closing the gap ADR 0027 deliberately left open.
- **The browser flow's requested scope does not include `api:oemr`.** `OpenIddictClientServiceCollectionExtensions.RequestedScopes`
  asks for `openid profile email offline_access` only (ADR 0005's "minimal scopes" for the OIDC client), and
  the seed (`scripts/seeds/authfacade-client.php`) grants the client exactly that set. Against a real OpenEMR
  server that enforces scope on its API, every proxied call will fail with an authorization error from OpenEMR
  itself — not from this proxy — until that scope is deliberately widened. This ADR does not widen it: doing
  so increases what a hijacked session cookie could reach, and ADR 0005's minimal-scope stance deserves its
  own explicit decision to reopen, not a side effect of adding a proxy. The unit tests prove the plumbing
  (token attached, forwarded, relayed back) against a fake OpenEMR that does not enforce scope; they do not
  prove a real clinical API call succeeds.
- An expired access token surfaces as whatever 401 shape OpenEMR itself returns, with no retry — a session
  that is otherwise still valid (the 12-hour cookie) can still fail API calls once the shorter-lived access
  token expires, until refresh is built.
- This proxy forwards to OpenEMR only. It is not the M3 gateway ROADMAP.md describes (routing to AuthFacade
  *and* future services like Patient Management) — it is scoped to the one thing a logged-in browser needs
  today. Whether M3's gateway subsumes this proxy or the two coexist is an open question for when M3 starts.
- ADR 0028's single-instance constraint already covers this proxy too: it reads the same per-process-keyed
  session cookie, so it introduces no new multi-instance gap beyond what 0028 already recorded.

## Alternatives considered

- **Duende.BFF.** Rejected for now; see Context. Revisit if the hand-rolled approach's gaps (no refresh, no
  revocation-on-logout) grow large enough that a maintained, complete implementation becomes worth the
  licensing question and the rework of ADR 0026/0027's existing code.
- **A fully hand-rolled proxy with `HttpClient`.** Rejected; see Context. No new information changed this
  once YARP was already the chosen tool for M3.
- **Widen the client's scope to include `api:oemr` now, so the proxy actually reaches protected data.**
  Rejected for this ADR: it is a security-posture decision (ADR 0005's minimal-scope stance), not a proxying
  one, and deserves to be made and recorded on its own rather than folded into "add a proxy."

## Related decisions

Builds on [0013](0013-pass-openemr-tokens-through.md) (passthrough, extended here to proxied calls),
[0024](0024-browser-authorization-code-flow-with-pkce.md) (why the browser never holds a bearer token),
[0026](0026-openiddict-client-drives-the-authorization-code-flow.md) and
[0027](0027-persist-tokens-in-the-session-cookie.md) (the session and the token it carries, which this ADR
finally reads). Does not reopen [0005](0005-oauth-client-provisioning-and-scopes.md)'s minimal-scope stance —
see Consequences. Covered by, not an addition to, [0028](0028-browser-login-and-session-assume-a-single-instance.md)'s
single-instance constraint. Relates to ROADMAP M3 (the gateway) without being it.
