# 0040. RP-initiated logout ends both AuthFacade's and OpenEMR's sessions

- **Status:** Accepted
- **Date:** 2026-11-01

## Context

Neither ADR 0024 nor ADR 0026 built a logout path. Both named the same open question and left it there: "RP-initiated
logout… OpenEMR's `league/oauth2-server` may not publish an `end_session_endpoint`… until that is checked
against the running stack, logout can only clear `authfacade_session` locally; it may not end OpenEMR's own
PHP session." ROADMAP.md's M1 still lists "revocation (logout) is unaddressed."

That question is now answered two ways, both against the real running stack:

1. **OpenEMR's discovery document does publish one.** `GET /oauth2/default/.well-known/openid-configuration`
   returns `"end_session_endpoint": "https://localhost:9300/oauth2/default/logout"`.
2. **Its exact contract**, read from the real PHP source inside the running `openemr` container
   (`src/RestControllers/AuthorizationController.php`'s `userSessionLogout()` — this repo vendors no OpenEMR
   PHP source of its own, so the running container's own copy is the only place to check):
   - `id_token_hint` is **required**. A request without one is refused outright with `invalid_request` /
     "Id token missing from request" (`Response::HTTP_BAD_REQUEST`) — confirmed by hitting the live endpoint
     directly. The token is taken apart for its `aud` (client ID) and `sub` (user) claims and compared
     against OpenEMR's own server-side "trusted user" record for that pair; nothing about the id_token's
     signature or expiry is checked here, only that its `nonce` claim matches the one on that record.
   - If found, the trusted-user record is deleted outright (`deleteTrustedUserById`) — this is OpenEMR's own
     server-side session for this client/user pair actually ending, not just a cookie disappearing.
   - `post_logout_redirect_uri` is optional, but only honored if it **exactly matches** (plain SQL `=`, not
     a host-based allow-list) the `logout_redirect_uris` column already stored for that `client_id`. A
     mismatch — including an unregistered or empty value — still ends the session; it just answers with a
     plain-text "You have been signed out" page instead of redirecting anywhere.
   - `state`, if given, is echoed back unvalidated as `?state=…` on the redirect.

AuthFacade never captured the `id_token` it already receives from OpenEMR during login into anything —
`OpenIddictTokenCapture` only ever stored `access_token`/`refresh_token`/`expires_at`. Without it, this
endpoint's one required parameter is unavailable at logout time no matter what else is built.

## Decision

1. **`OpenIddictTokenCapture` also stores `id_token`**, read from
   `OpenIddictClientAspNetCoreConstants.Tokens.BackchannelIdentityToken`, into the session cookie alongside
   the access/refresh token it already carries (ADR 0027). A session signed in before this change has none
   — handled explicitly, see point 3.
2. **`GET /logout`** (`LogoutEndpoints.cs`, mapped and routed through the gateway the same way
   `/oidc/login`/`/callback` are): signs the `AuthFacadeSession` cookie out immediately, unconditionally,
   before anything else — the same "invalidate first" order OpenEMR's own handler uses even on its
   exception path. AuthFacade's own notion of the session ends regardless of what happens next.
3. **No `id_token` captured → redirect straight to `BrowserFlowOptions.DefaultReturnUrl`, nothing sent to
   OpenEMR.** Covers "never signed in" and "a session that predates this decision" identically: there is
   nothing OpenEMR needs to end on AuthFacade's behalf in either case, and no `id_token` to send even if
   there were — sending a bare request would just get the same `invalid_request` confirmed above.
4. **An `id_token` present → redirect to `{Issuer}/logout?id_token_hint={idToken}&post_logout_redirect_uri={DefaultReturnUrl}`.**
   `post_logout_redirect_uri` is the *same fixed* `BrowserFlowOptions.DefaultReturnUrl` used in point 3, not
   a caller-supplied `returnUrl` the way `/oidc/login`'s own return URL is — OpenEMR's exact-match
   requirement (Context, point 2) makes a dynamic, allow-listed value pointless here: anything other than
   the one value actually registered would silently lose the redirect. Landing back on the exact page the
   user asked to leave would also be the wrong default regardless.
5. **The seed registers that same fixed URL as `logout_redirect_uris`**, from a new required
   `AUTHFACADE_POST_LOGOUT_REDIRECT_URI` environment variable, computed identically to
   `BrowserFlow:DefaultReturnUrl` (Compose does not share one service's environment values with another, the
   same reason `AUTHFACADE_REDIRECT_URI` is computed twice already). The existing reseed-on-change mechanism
   (ADR 0037) covers this column for free: any `docker compose up` that recreates `seed` refreshes it along
   with everything else.
6. **No new OpenIddict/OpenIddict.Client machinery.** Unlike the authorization-code flow (ADR 0026), RP-
   initiated logout is a single outbound redirect this code builds directly — `{Issuer}/logout` plus two
   query parameters — not a flow OpenIddict.Client has its own API surface for.
7. **The WebApp dashboard gets a "Log out" button** (`Home.razor`, signed-in state), a plain `<a>`-style
   navigation to `/logout`, the same pattern the Login page already uses for `/oidc/login`. `LoginUi` itself
   needs no change: it has no signed-in state to log out of.

## Consequences

- A user who clicks "Log out" actually ends their OpenEMR session, not just AuthFacade's local cookie — the
  gap both ADR 0024 and ADR 0026 left open is closed.
- Verified against the real Compose stack: the seed registers `logout_redirect_uris` exactly matching what
  `/logout` sends; `/logout` with no session redirects straight to the Login UI (through the gateway, with
  `Secure` correctly set on the cleared cookie; hitting AuthFacade's own direct port, correctly without
  `Secure`) without ever reaching OpenEMR. The full round trip with a real `id_token` — login, then logout,
  then confirming OpenEMR's own trusted-user record is actually gone — is covered in-process by
  `LogoutEndpointTests` against a fake OpenEMR server; a real browser click-through was not run in this
  session (no access to the stack's admin password).
- A stolen `AuthFacadeSession` cookie still works until its own 12-hour expiry if the legitimate user never
  clicks "Log out" — this decision ends sessions on request, it does not add a way to force-end one from
  outside the browser that holds it. That gap (ADR 0024's "no server-side session store," reaffirmed by ADR
  0026) is unchanged and not reopened here.
- `state` is not used: there is no dynamic value this flow needs to round-trip (point 4), so OpenEMR's
  redirect always carries an empty `?state=`, harmless and unread by `LoginUi`.

## Not covered

- **Force-ending a session from outside the browser that holds it** (an admin action, or an incident
  response to a suspected-stolen cookie). Needs a server-side session record to revoke against, which ADR
  0024/0026 deliberately did not build. A real gap, not addressed by this decision.
- **Front-channel logout** (OpenEMR calling back into AuthFacade to confirm its session ended, the other
  half of OIDC session management beyond what this decision implements). Not confirmed whether OpenEMR
  supports it; not investigated here.

## Alternatives considered

- **A dynamic, caller-supplied `returnUrl` for logout, allow-listed the same way `/oidc/login`'s is.**
  Rejected: OpenEMR's own exact-match check on `logout_redirect_uris` (Context, point 2) makes this
  pointless — only the one value actually registered is ever honored, so a host-based allow-list here would
  validate something OpenEMR would then ignore anyway.
- **Skip OpenEMR's `end_session_endpoint` and only clear the local cookie**, deferring RP-initiated logout
  again. Rejected now that its existence and exact contract are both confirmed against the real server —
  the thing ADR 0024/0026 were waiting to confirm before building this is no longer unknown.
- **Validate the `id_token`'s signature/expiry before sending it as `id_token_hint`.** Rejected as
  unnecessary: OpenEMR's own handler does not check either (Context, point 2) — it already receives a
  token in a session cookie protected by ASP.NET Core's Data Protection, so there is nothing a second,
  local check here would add.

## Related decisions

Closes the logout question ADR 0024 and ADR 0026 each left open. Extends `OpenIddictTokenCapture` (ADR
0027's token-capture mechanism) to a third token. Reuses the gateway routing and HTTPS-dependent `Secure`
cookie behavior ADR 0033/0039 established — `/logout`, like `/oidc/login`, only reaches OpenEMR through the
gateway's HTTPS address in the real flow. Leaves the "no server-side session store" stance of ADR
0024/0026 unchanged; does not address the stolen-cookie revocation gap those decisions already named.
