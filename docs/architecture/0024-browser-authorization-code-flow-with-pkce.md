# 0024. Browser authorization-code flow with PKCE establishes a cookie session

- **Status:** Superseded by [0026](0026-openiddict-client-drives-the-authorization-code-flow.md)
- **Date:** 2026-10-30

## Context

ADR 0001 named the browser authorization-code flow with PKCE as the production target and left it
unimplemented; the password grant was kept only as a stand-in. ADR 0014 already anticipated it: PKCE is the
one thing that makes OpenEMR check the client secret for this client, since
`ClientRepository::validateClient()` checks the secret only for the `authorization_code` grant.

The `authfacade` OAuth client is already provisioned for this. `scripts/seeds/authfacade-client.sql` sets
`grant_types = 'authorization_code'` and a `redirect_uri` of `http://localhost:${AUTHFACADE_HTTP_PORT}/callback`
(ADR 0005); nothing has used either value yet. A browser hitting OpenEMR's
`oauth2/default/authorize` with no PHP session gets OpenEMR's own `login.php`, the same screen its
SMART-on-FHIR and patient-portal clients already use. AuthFacade does not need, and must not build, its own
credential-collecting page: a front-end "Log in with OpenEMR" button only needs to navigate the browser to an
AuthFacade endpoint that starts this flow.

Unlike the password and refresh grants (ADR 0013), this flow ends with a browser redirect, not a request
AuthFacade can answer with a JSON body. Something has to carry OpenEMR's token response across that redirect
and back to later requests.

## Decision

1. **A third grant on the same broker.** `IIdentityProvider` gets `AuthorizationCodeGrantAsync(code,
   codeVerifier, redirectUri, ct)`, implemented in `OpenEmrIdentityProvider` beside `PasswordGrantAsync` and
   `RefreshGrantAsync`, posting `grant_type=authorization_code` to the same `oauth2/default/token` path and
   reusing the existing form-posting and token-parsing code. `TokenGrantHandler` gets a third `HandleAsync`
   overload that funnels into the same status-mapping and token-verification method the other two grants use.
   `CircuitBreakingIdentityProvider` (ADR 0017) covers it for free, since it wraps the same port.
2. **`OpenEmrToken` gains `id_token`.** `ReadToken` already parses `access_token`, `token_type`, `expires_in`
   and `refresh_token` from OpenEMR's body; it now also reads `id_token` when present. The password and
   refresh grants ignore it, as they do today; this flow is the first to use it.
3. **Two new unauthenticated, browser-navigation endpoints, outside `/api/v1`.** Like `/health`
   (`SystemEndpoints`), they carry no API version and are excluded from the OpenAPI document, since a browser
   navigates to them rather than a client calling them as an API.
   - **`GET /login`** generates `state`, `nonce` and a PKCE `code_verifier` (and its `code_challenge`, S256),
     reads an optional `returnUrl` query parameter, and packs `{state, nonce, code_verifier, returnUrl}` into
     one short-lived cookie (`authfacade_oidc`: HttpOnly, SameSite=Lax, ~5-minute `Max-Age`, `Secure` only when
     the request itself is HTTPS — see point 10), encrypted with `IDataProtectionProvider` under a fixed
     purpose string. It then issues a 302 to `{OpenEmr:Issuer}/authorize` — `Issuer`, not `BaseUrl`: the
     browser must reach this URL itself, and inside Compose `BaseUrl` is the container-internal
     `http://openemr:80` AuthFacade uses for server-to-server calls, while `Issuer` is the publicly-reachable
     `https://localhost:9300/oauth2/default` (ADR 0012 drew the same distinction for JWKS). The query string
     carries `response_type=code`, `client_id`, the new `OpenEmr:RedirectUri` setting,
     `scope=openid profile email offline_access` (the scope the client is already seeded with — not
     `OpenEmr:Scope`, which is the password grant's `openid api:oemr`), `state`, `nonce`, `code_challenge`
     and `code_challenge_method=S256`.
   - **`GET /callback`** matches the seeded `redirect_uri`'s path. It reads and immediately clears the
     `authfacade_oidc` cookie, rejects the request if `state` does not match, and otherwise calls the new
     `TokenGrantHandler` overload with `code`, `code_verifier` and `OpenEmr:RedirectUri`. On success, it
     verifies the `id_token` the same way `/me` verifies a bearer token (signature against OpenEMR's JWKS,
     issuer, audience) and additionally checks its `nonce` claim against the one stashed in the cookie — a
     check specific to this flow, not part of the generic verifier. It then sets `authfacade_session`
     (HttpOnly, SameSite=Lax, same conditional `Secure` as point 10), encrypted the same way, holding OpenEMR's
     token response, and redirects to `returnUrl`.
   - **A failure redirects to the same place, with a short, generic error code — never OpenEMR's own error
     text.** A missing or mismatched `state`, a rejected grant, a token or `id_token` that fails verification,
     and an `error` query parameter OpenEMR itself appended (for example, the user declining consent) each map
     to one of three codes appended as `?error=`: `access_denied` (OpenEMR reported it), `invalid_request`
     (the flow cookie was missing, expired or did not match, so the real `returnUrl` could not be recovered),
     or `login_failed` (anything else — a rejected grant, an upstream fault, or a token that did not verify).
4. **Two new required settings govern where the browser is sent, under a new `BrowserFlow` section.**
   `BrowserFlow:DefaultReturnUrl` is used whenever `returnUrl` is absent, or the flow cookie cannot be read at
   all (so an `invalid_request` failure always has somewhere to go). `BrowserFlow:ReturnUrlAllowedHosts` is
   the allow-list a caller-supplied `returnUrl` must resolve to, on both `/login` and `/callback`, checked
   before it is ever stashed in a cookie or used in a redirect; a `returnUrl` outside it is replaced by
   `BrowserFlow:DefaultReturnUrl` rather than rejected outright, so a bad link degrades to the default page
   instead of failing the whole flow.
5. **A new required setting, `OpenEmr:RedirectUri`.** Bound from the same `AUTHFACADE_REDIRECT_URI`
   environment variable `docker-compose.yml` already sets for the seed, so the value used to build the
   authorize URL and the value registered in `oauth_clients.redirect_uri` cannot drift apart.
6. **No server-side session store.** Both cookies carry their payload encrypted via ASP.NET Core's Data
   Protection API; AuthFacade adds no database row and no cache entry for a session, keeping ADR 0001 and
   ADR 0007's stance that AuthFacade holds no identity data of its own. The cookie, not a store, is the
   session.
7. **AuthFacade still issues no tokens of its own.** The session cookie is a transport for OpenEMR's token
   response, not a new token format; this does not reopen ADR 0002/0013.
8. **The existing bearer path is untouched.** `/me` and any other API client keep using
   `Authorization: Bearer` exactly as ADR 0013 describes. The cookie is an additional credential path for a
   browser SPA, not a replacement.
9. **The button's page is a separate project, `OpenEmrNext.LoginUi`**, under `src/Services/LoginUi/`, sibling
   to AuthFacade rather than part of it — it holds no project reference to any AuthFacade assembly and knows
   AuthFacade only by its configured `AuthFacade:BaseUrl`. It is one static page: a link to
   `{AuthFacade:BaseUrl}/login`, forwarding an optional `returnUrl` query parameter unchanged, and rendering
   this decision's three `error` codes as fixed, friendly messages. It never echoes a raw, unrecognized
   `error` value — anything it does not recognize gets the same generic message `login_failed` would.
10. **`Secure` is set only when the request itself is HTTPS, on both cookies.** The local stack still runs
    over plain HTTP (ADR 0020); a cookie marked `Secure` unconditionally would never come back from the
    browser on it, so `/login` and `/callback` would be unable to read their own cookies in the stack as it
    ships today. Checking `HttpContext.Request.IsHttps` keeps the property in any deployment that terminates
    TLS at AuthFacade itself, and degrades safely rather than breaking the flow outright where it does not.

## Consequences

- A browser client never holds OpenEMR's access or refresh token in JavaScript; both live only in an HttpOnly
  cookie, out of reach of an XSS payload that cannot also read `authfacade_session` server-side.
- The authorization-code grant is the one path where OpenEMR actually checks `OpenEmr:ClientSecret`
  (ADR 0014); a wrong or leaked secret now matters here even though it does nothing for the password or
  refresh grants.
- Cookies are only as durable as the Data Protection key ring. A single AuthFacade instance with the default,
  ephemeral key ring loses all outstanding sessions on restart — the same class of gap ADR 0017 and ADR 0018
  already accept for in-memory circuit-breaker and rate-limit state. A multi-instance deployment needs a
  shared, persisted key ring for sessions to survive a restart or to work at all across instances; this
  decision does not set one up.
- An attacker-supplied `returnUrl` on `/login` would be an open-redirect vector if forwarded unchecked to
  `/callback`'s final redirect; `BrowserFlow:ReturnUrlAllowedHosts` closes that by substituting
  `BrowserFlow:DefaultReturnUrl` for anything outside it.
- `OpenEmrTokenVerifier` (or its caller) grows a code path that also checks an `id_token`'s `nonce` claim,
  which the plain bearer-token path has no equivalent of.
- `/login` and `/callback` carry no username or password, so ADR 0018's per-address rate limit on
  `/connect/token` does not apply to them as written; they are unprotected against abuse until a decision
  covers them (see Not covered).
- A deployment that terminates TLS in front of AuthFacade, rather than inside it, needs forwarded-header
  handling before `Request.IsHttps` reflects the public connection; until then, point 10's cookies ship
  without `Secure` even though the browser's own connection is HTTPS. ADR 0020 already leaves forwarded
  headers as a gap for the stack generally.

## Not covered

- **RP-initiated logout.** OpenEMR's `league/oauth2-server` may not publish an `end_session_endpoint` in its
  discovery document. Until that is checked against the running stack, logout can only clear
  `authfacade_session` locally; it may not end OpenEMR's own PHP session.
- **A shared Data Protection key ring for multiple instances.**
- **Rate limiting `/login` and `/callback`.**
- **Wiring `/me` or any other endpoint to accept `authfacade_session` as an alternative to a bearer token.**
  Today only `/login` and `/callback` read or write it.

## Alternatives considered

- **Return tokens to the SPA in a redirect fragment, for it to store itself.** Rejected. It puts OpenEMR's
  access and refresh tokens in reach of any XSS on the SPA, where a cookie-based session does not.
- **A server-side session store (database or cache) keyed by an opaque session ID.** Deferred, not rejected.
  It would survive a restart without a persisted Data Protection key ring, but it adds a stateful dependency
  the project has avoided so far — ADR 0001 rejected OpenIddict for exactly this reason. Revisit if the
  key-ring gap above proves to matter in practice.
- **A facade-issued session JWT instead of passing OpenEMR's token response through.** Rejected. It reopens
  ADR 0002 and ADR 0013's already-settled decision that AuthFacade issues no tokens of its own.
- **Skip PKCE since the client is already confidential.** Rejected. PKCE is cheap, is explicitly named in
  ADR 0001 as part of the production target, and adds defense-in-depth on top of, not instead of, the client
  secret OpenEMR now actually checks for this grant (ADR 0014).

## Related decisions

Implements the production target ADR 0001 deferred. Extends the grant-handling shape ADR 0023 set up for the
refresh grant to a third grant. Reuses the JWKS-backed verification ADR 0012 and ADR 0013 established, for the
`id_token` as well as the access token. Confirms ADR 0005's client provisioning (`grant_types` and
`redirect_uri`) needs no change for this flow. Does not reopen ADR 0002 or ADR 0013's decision that AuthFacade
issues no tokens of its own.
