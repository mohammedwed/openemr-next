# 0044. A server-side session store backs the cookie, so a session can be revoked

- **Status:** Accepted
- **Date:** 2026-11-01
- **Builds on:** [0027](0027-persist-tokens-in-the-session-cookie.md), [0040](0040-rp-initiated-logout.md), [0043](0043-authfacade-session-state-in-a-dedicated-mariadb-database.md)

## Context

ADR 0040 built logout, but named what it deliberately did not: "no way to force-end a session from outside
the browser that holds it (an admin action, or a stolen-cookie response) — same root cause as item #3
[ADR 0028]." The root cause is architectural, not missing code: the `AuthFacadeSession` cookie's ticket is
self-contained (ADR 0027) — the whole thing, including the real OpenEMR access/refresh token, is encrypted
and handed to the browser. Whoever can present a valid, undecrypted-by-anyone-but-AuthFacade copy of that
cookie *is* an authenticated session. There is no server-side record that a given session exists, so there
is nothing to delete to make one specific session — including a copy an attacker holds, which the
legitimate user has no way to reach by visiting the site themselves — stop working. `/logout` (ADR 0040)
only ever ends the session the caller's own request already carries; it cannot help with a copy sitting
somewhere else.

ADR 0043 gave AuthFacade's own session-related state a real, persistent database for exactly this kind of
problem, closing two of the three gaps ADR 0028 named. This is the natural next thing to put there: ASP.NET
Core's cookie authentication has a built-in extensibility point for precisely this — `ITicketStore`,
assigned to `CookieAuthenticationOptions.SessionStore`. When set, the cookie no longer carries the ticket
itself, only an opaque key; the real ticket is fetched from the store on every request. Deleting a row
*is* revocation, with no cooperation from whichever browser holds a cookie referencing it.

OpenEMR itself provides no way to help further here. Its OIDC discovery document publishes no
`revocation_endpoint` (confirmed live), and its own source
(`AccessTokenRepository`/`RefreshTokenRepository`) has internal revoke methods but no REST route that
calls them — RFC 7009 token revocation is not implemented as a reachable endpoint. `end_session_endpoint`
(ADR 0040) is a browser-driven redirect flow, not a server-to-server API, so AuthFacade cannot use it to
end a session it is not the browser for, either. Whatever this ADR builds, the underlying OpenEMR
access/refresh token itself stays valid at OpenEMR until it naturally expires — a pre-existing limitation,
not something this change introduces or claims to fix.

## Decision

1. **`AuthFacadeSessionStore` implements `ITicketStore`**, assigned to the `AuthFacadeSession` cookie
   scheme's `SessionStore` via a post-registration `Configure<IServiceScopeFactory, IDataProtectionProvider>`
   step (`OpenIddictClientServiceCollectionExtensions`): the store is a singleton, so each method opens its
   own short-lived scope to resolve `AuthFacadeDbContext`, the same pattern a singleton background service
   uses for a scoped dependency, rather than the store needing a scoped lifetime of its own. Ticket bytes
   are protected with Data Protection under a dedicated purpose string before being stored — the same
   confidentiality guarantee the cookie's own value would have had, now applied to a database row instead.
2. **One new table, `AuthFacadeSessions`, in the same dedicated database ADR 0043 introduced** — `Key`
   (the opaque value the cookie now carries), `Subject` (indexed, so every session for one user can be
   found together), `ProtectedTicket`, `ExpiresUtc`. No migration, same as every other table there: created
   by `EnsureCreatedAsync`.
3. **No application code at `/callback` or `/logout` changes.** Both already call the standard
   `HttpContext.SignInAsync`/`SignOutAsync`; the cookie handler itself calls `StoreAsync`/`RemoveAsync`
   automatically once `SessionStore` is configured. This is the entire reason `ITicketStore` is the right
   mechanism here instead of a hand-rolled revocation check: the existing sign-in/sign-out code did not
   need to know a store exists.
4. **`POST /sessions/revoke-all`**, authenticated the same way `/api/v1/me` is (the cookie scheme is
   already the API's only `DefaultScheme`, ADR 0035) — deletes every session row for the caller's own
   subject, not just the one making the request, then signs the caller's own current cookie out too for
   immediate effect in that browser. This is the actual, achievable half of ADR 0028's "an admin action, or
   a stolen-cookie response": the stolen-cookie response, self-service, needing no new authorization model.
   Revoking an arbitrary *other* user's session (the admin-action half) is explicitly not built here —
   see Alternatives.

## Consequences

- A session is now something that can be ended by a process other than the browser holding it. The
  specific threat ADR 0028 named — a stolen `AuthFacadeSession` cookie — is fully addressed: the attacker
  never had the raw OpenEMR token to begin with (ADR 0027 keeps it inside the encrypted ticket), only a
  cookie referencing a session row; deleting that row makes the stolen copy fail its very next request.
- The underlying OpenEMR access/refresh token is not revoked at OpenEMR's end — OpenEMR exposes no
  server-to-server revocation endpoint to call (see Context). A pre-existing limitation, unchanged by this
  work, now stated as a limit of `/sessions/revoke-all` specifically rather than only a general gap.
- Verified live against the real Compose stack: the `AuthFacadeSessions` table is created in the dedicated
  `authfacade` database (ADR 0043) with the expected columns and an index on `Subject`; `/sessions/revoke-all`
  reaches AuthFacade through the gateway and answers 401 unauthenticated, the same way `/api/v1/me` does.
  Verified with unit tests against a fake OpenEMR server that an authenticated call actually deletes the
  caller's own session (subsequent `/api/v1/me` calls fail) and every other session sharing its subject
  (a second, independently-logged-in browser against the same `TestApi`), while leaving a different
  subject's session untouched.
- Same "no migrations" limitation ADR 0043 already documented applies to this new table too:
  `EnsureCreatedAsync` only checks whether *any* table already exists in the target database, not whether
  the schema is complete, so an existing `authfacade` database from before this change needed dropping and
  recreating once, the same one-time step ADR 0043's own charset fix needed.
- Revoking an arbitrary other user's session — the admin-action half of the original gap — is still not
  possible. AuthFacade has no authorization model to decide who may revoke whose session (the same
  pre-existing gap the README's "Known gaps" already names for `/api/v1/me`); building one is out of scope
  here and belongs with that gap's own eventual resolution, not bundled into this change.

## Alternatives considered

- **`OnValidatePrincipal`, checking a separate revoked-session blocklist against an otherwise
  self-contained ticket.** Rejected: this still bakes the whole ticket (including the real OpenEMR token)
  into the cookie, so a revoked ticket's claims are still disclosed to whoever holds the cookie even after
  revocation — only the ASP.NET Core *authentication* decision changes, not what was already handed to the
  browser. `ITicketStore` removes the ticket from the cookie entirely, which is the stronger property, and
  needed no new check wired into the authentication pipeline besides assigning `SessionStore`.
- **Admin-initiated revocation of another user's session**, closing the other half of ADR 0028's named
  gap in the same change. Rejected for now: it needs an authorization model (who counts as an admin, and
  how that is proven) this project does not have, and inventing one as a side effect of this ADR would be
  speculative work ahead of whatever `/api/v1/me`'s own "authorization for OpenEMR data is not built" gap
  eventually resolves to.
- **Asking OpenEMR to revoke the underlying token when a session is revoked.** Rejected: not possible today
  — see Context. Tracked as a pre-existing, unchanged limitation, not a new scope decision.

## Related decisions

Builds on [0027](0027-persist-tokens-in-the-session-cookie.md) (the ticket this now moves out of the
cookie) and [0043](0043-authfacade-session-state-in-a-dedicated-mariadb-database.md) (the database it moves
into). Closes the "force-ending a session from outside the browser that holds it" half of the gap
[0028](0028-browser-login-and-session-assume-a-single-instance.md) and
[0040](0040-rp-initiated-logout.md) both named, for the stolen-cookie scenario specifically; the
admin-action half remains open, tied to the same authorization gap `/api/v1/me` already has.
