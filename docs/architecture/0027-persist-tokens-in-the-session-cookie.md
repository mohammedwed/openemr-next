# 0027. The session cookie carries the real OpenEMR access and refresh token

- **Status:** Accepted
- **Date:** 2026-10-31
- **Builds on:** [0026](0026-openiddict-client-drives-the-authorization-code-flow.md)

## Context

A browser-based client cannot safely be handed a bearer token directly: storing it in JS-reachable memory or
`localStorage` exposes it to XSS, which is exactly the reasoning the OIDC browser flow (ADR 0026) already
avoids for the session itself, by keeping only an opaque `HttpOnly` cookie on the browser side. But a session
cookie scoped to AuthFacade's own origin has no standing with any other service — it cannot be presented to
OpenEMR's API, or to any other microservice that would trust OpenEMR's tokens, the way a `Bearer` credential
can (ADR 0013). Closing that gap means AuthFacade acting as a token-handling reverse proxy for a browser
client: the browser only ever holds the session cookie, and AuthFacade attaches the real token server-side
when it forwards a call on the browser's behalf.

That requires AuthFacade to actually have the token after login. It already does, momentarily: OpenIddict.Client
performs the real code-for-token exchange against OpenEMR during `/callback`, and keeps the resulting
`access_token`, `refresh_token` and expiration on the `AuthenticateResult` it hands back (stored under its own
`OpenIddictClientAspNetCoreConstants.Tokens.*` names, confirmed by reading OpenIddict's own
`OpenIddictClientAspNetCoreHandler` source). `HandleCallbackAsync` discarded all of it — it only ever copied
`principal.Claims` (the `id_token`'s claims) into the session identity. Once that endpoint returns, the only
real artifact of the login is gone.

## Decision

1. **`OpenIddictClientEndpoints.HandleCallbackAsync` now builds an `AuthenticationProperties` for the session
   sign-in**, via a new `CaptureTokens(AuthenticateResult)` helper, instead of signing in with none. It reads
   `OpenIddictClientAspNetCoreConstants.Tokens.BackchannelAccessToken`, `.RefreshToken` and
   `.BackchannelAccessTokenExpirationDate` off the OpenIddict result (`result.Properties.GetTokenValue(...)`)
   and re-stores them under the plain names `access_token`, `refresh_token` and `expires_at` via the standard
   `AuthenticationProperties.StoreTokens(...)` — the same convention `OAuthHandler`/`OpenIdConnectHandler` use
   with `SaveTokens = true`, so a later caller reads them back with the ordinary
   `HttpContext.GetTokenAsync(SessionScheme, "access_token")` extension, not an OpenIddict-specific API.
2. **No new store.** The tokens live inside the `AuthFacadeSession` cookie's own encrypted ticket payload —
   the same Data-Protection-backed mechanism ASP.NET Core's cookie authentication already uses to protect the
   ticket, and the same preference ADR 0024 already stated for this project (encrypted cookies over a
   server-side session store) for state that only needs to survive one browser's session.
3. **Nothing reads these tokens yet.** This ADR only makes them survive past `/callback`; the token-handling
   proxy endpoint that would attach them to an outbound call on the browser's behalf is a separate, later
   piece of work.

## Consequences

- The `AuthFacadeSession` cookie now carries real OpenEMR credentials, not just identity claims. It is already
  `HttpOnly` and `SameSite=Lax` (ADR 0026); nothing about that changes, but the cookie's blast radius if it
  were ever decrypted is larger than before — it was already meant to be opaque and un-decryptable outside
  this service, so this is a difference in degree, not in kind.
- Cookie size grows by roughly the size of an OpenEMR access token, a refresh token, and a timestamp, plus
  Data Protection's own overhead. This stays well under typical per-cookie limits for OpenEMR's current token
  shape, but a future OpenEMR token with substantially more claims could approach them; no limit is enforced
  here.
- The refresh token now outlives the access token inside the same cookie, so a later proxy can refresh
  silently against OpenEMR's token endpoint without forcing a new browser login — the reason both were
  captured, not just the access token.
- Verified with a real round trip (`OpenIddictClientCallbackTests.Successful_callback_persists_the_real_openemr_access_and_refresh_token`):
  a genuine OpenIddict.Client exchange against `FakeOpenEmrServer`, a real encrypted cookie, and retrieval
  through the standard `HttpContext.GetTokenAsync` extension from a test-only endpoint — not a decrypted
  inspection of the cookie's bytes.

## Alternatives considered

- **A server-side session store** (a new table keyed by a session ID in the cookie). Rejected for now:
  more moving parts (a store, a cleanup/expiry policy, a lookup on every request) for no benefit this project
  currently needs, and it departs from the cookie-only pattern ADR 0024 already established here. Worth
  revisiting if cookie size or revoke-by-deletion ever becomes a real constraint.
- **Re-fetch the token from OpenEMR on each proxied call** (e.g. token introspection). Rejected for the same
  reason ADR 0013 rejected it for `/me`: a network round trip per call, where holding the token (safely,
  server-side) works offline between refreshes.

## Related decisions

Builds directly on [0026](0026-openiddict-client-drives-the-authorization-code-flow.md)'s session cookie and
[0013](0013-pass-openemr-tokens-through.md)'s decision that AuthFacade passes OpenEMR's own tokens through
unchanged rather than issuing its own. Sets up, but does not implement, the token-handling proxy pattern this
project's BFF discussion names as the fix for a browser client that cannot safely hold a bearer token itself.
