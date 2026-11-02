# 0047. The identity bridge: AuthFacade's design, end to end

- **Status:** Accepted. Describes the finished design; introduces no new behavior. Closes M1's exit
  criterion ("the ADR is merged") in ROADMAP.md.
- **Date:** 2026-11-02
- **Synthesizes:** every ADR listed under Related decisions below.

## Context

ROADMAP.md's M1 ("Harden the identity bridge") was never one decision — it was roughly twenty, made one
at a time across three weeks, each solving a single named gap: a session cookie, then a BFF proxy, then
rate limiting, then TLS, then logout, then circuit breakers, then an audit log, then where session state
actually lives, then whether it can be revoked, refreshed, and whether its own certificates survive a
restart. Each of those ADRs is correct in isolation and none needed to be wrong for this one to exist. But
nowhere does a single document say what the finished bridge actually *is* — what it guarantees end to
end, and what it deliberately does not. Understanding that today means reading about twenty files and
assembling the picture yourself.

This ADR is that document. It is a capstone, not a new decision: it changes no code and overrides nothing
below it. Where it disagrees with a stated consequence of an earlier ADR, that earlier ADR is right and
this one is describing it badly — fix the description, not this ADR's authority over it.

"AuthFacade" is this project's name for the identity bridge itself: the one component standing between
OpenEMR's own, unmodified login system and everything new being built around it, so nothing new ever has
to speak OpenEMR's authentication dialect directly.

## Decision — the design, as built

### The shape

A browser talks to exactly one external address, the gateway, for everything — the dashboard and
AuthFacade's own login, callback, logout, and session endpoints alike ([0033](0033-gateway-fronts-webapp-and-authfacade.md),
[0039](0039-gateway-terminates-tls-authfacade-trusts-forwarded-headers.md)). The gateway terminates TLS and
routes by path; it makes no identity decisions of its own.

```text
                        +------------------------+
    browser --HTTPS--> |     Gateway (YARP)     |  terminates TLS, routes by path
                        +-----------+------------+
                                    |
                        +-----------v------------+
                        |   AuthFacade (.NET)    |  BFF: OIDC login, session cookie,
                        |     BFF for browsers   |  proxies browser calls to OpenEMR
                        +------+-------------+---+
                               |             |
           OAuth2 (code, or a  |             |  session state only: OpenIddict's state
           proxied call with   |             |  tokens, the Data Protection key ring, and
           the token attached) |             |  each session's own revocable ticket
                               v             v
                  +------------------+   +------------------------+
                  | OpenEMR (legacy) |   | mysql: authfacade DB  |
                  +--------+---------+   +------------------------+
                           |
                           v
                  +-------------------+
                  | mysql: openemr DB |
                  +-------------------+
```

AuthFacade is a BFF (backend-for-frontend), not a general-purpose gateway and not an identity provider of
its own. It holds no user accounts, no passwords, no ACLs. OpenEMR owns every one of those, unmodified;
AuthFacade delegates to it rather than duplicating any of it
([0003](0003-delegate-credential-verification-to-openemr.md), superseded in mechanism by
[0035](0035-password-grant-and-bearer-auth-are-removed.md) but not in principle). `authfacade` and
`openemr` are two databases, with two separate least-privilege users, on the same `mysql` Compose service —
not two servers, and not shared data: one holds OpenEMR's own records, the other holds nothing but
AuthFacade's session-related state ([0043](0043-authfacade-session-state-in-a-dedicated-mariadb-database.md)).

### The login flow

A browser follows a link to `/oidc/login`, which challenges into the authorization-code flow with PKCE
against OpenEMR's own, already-conformant OAuth2/OIDC server — driven by OpenIddict.Client, a library,
rather than hand-rolled PKCE and cookie code
([0024](0024-browser-authorization-code-flow-with-pkce.md), [0026](0026-openiddict-client-drives-the-authorization-code-flow.md)).
`/callback` redeems the code, validates the `id_token`'s signature, issuer, audience and nonce against
OpenEMR's own signing key, and on success signs the browser into the `AuthFacadeSession` cookie scheme —
and only that scheme. **The browser is never, under any path, handed a bearer token.** The real OpenEMR
access and refresh token OpenIddict obtained during the exchange are carried inside the session's own
ticket, never exposed to the client that holds the cookie referencing it
([0027](0027-persist-tokens-in-the-session-cookie.md)).

### What the session actually is

The `AuthFacadeSession` cookie itself carries only an opaque key. The real ticket — claims, and the real
OpenEMR tokens inside them — lives server-side, in one row per session, in the dedicated `authfacade`
database, via ASP.NET Core's own `ITicketStore` extensibility point
([0043](0043-authfacade-session-state-in-a-dedicated-mariadb-database.md), [0044](0044-session-store-backs-the-cookie-for-revocation.md)).
This is what makes a session something real rather than just a self-decrypting blob the browser happens
to hold:

- It **survives a restart**: the state-token store, the Data Protection key ring that protects the
  ticket, and the OpenIddict encryption/signing certificates that protect the state token exchanged
  during login are all persisted files or database rows now, not per-process, regenerated-on-every-start
  artifacts ([0043](0043-authfacade-session-state-in-a-dedicated-mariadb-database.md),
  [0046](0046-persisted-openiddict-certificates.md)) — verified live, more than once, by actually
  restarting the container mid-session and mid-login and confirming nothing broke.
- It **can be revoked**: deleting the row is revocation, with no cooperation from whichever browser holds
  a cookie referencing it. `POST /sessions/revoke-all` is the self-service answer to a suspected stolen
  cookie; `POST /sessions/revoke/{subject}` is the admin-action equivalent, gated by a config-driven
  allowlist ([0044](0044-session-store-backs-the-cookie-for-revocation.md), [0045](0045-silent-refresh-and-admin-session-revocation.md)).
- It **renews itself**: when the stored access token is expired or close to it, the BFF proxy silently
  redeems the refresh token before the browser ever notices, falling back to the stale token (and letting
  OpenEMR's own 401 decide) if the refresh itself fails ([0045](0045-silent-refresh-and-admin-session-revocation.md)).
- It **ends cleanly, on both sides**: `/logout` signs the local session out and, when it captured an
  `id_token`, redirects to OpenEMR's own RP-initiated logout endpoint too — a contract (a required
  `id_token_hint`, an exact-match `post_logout_redirect_uri`) read directly from OpenEMR's own source,
  since neither ADR 0024 nor ADR 0026 had confirmed it existed ([0040](0040-rp-initiated-logout.md)).

### Reaching OpenEMR's data

A browser session reaches OpenEMR's own API through `/bff/openemr/**`, a reverse proxy that attaches the
session's real access token as `Authorization: Bearer`, server-side — the browser, which never held that
token, never sends it either ([0029](0029-yarp-proxies-browser-sessions-to-openemrs-api.md)). Today this
proxy's only destination is OpenEMR itself; once a second backend service exists, it is designed to gain
one more cluster pointed at the future API gateway rather than learning every new service by name
([0030](0030-bff-proxy-forwards-to-the-gateway-not-each-service.md)) — not yet built, because there is
nothing beyond OpenEMR to reach yet.

### Defense in depth

None of the above is the only thing standing between a request and OpenEMR:

- **Rate limiting**: `/oidc/login` and `/callback` share one budget per client address
  ([0038](0038-rate-limit-login-and-callback-per-client-address.md)).
- **Transport**: the gateway terminates TLS, redirects plain HTTP, and applies HSTS on every route;
  AuthFacade trusts forwarded headers from one fixed Docker subnet only, via a hand-rolled check —
  ASP.NET Core's own built-in mechanism was confirmed not to restrict anything correctly on the installed
  runtime ([0039](0039-gateway-terminates-tls-authfacade-trusts-forwarded-headers.md)).
- **Circuit breakers**: the token exchange, the JWKS fetch, and the BFF proxy's forwarded calls each have
  one, so a failing OpenEMR degrades to a fast, generic failure instead of every caller separately waiting
  out a timeout ([0041](0041-circuit-breakers-guard-the-remaining-openemr-calls.md)).
- **An audit trail**: login outcomes, BFF proxy access, and admin-initiated revocation are logged to one
  filterable category, with no credential, token, or request/response body ever in it
  ([0042](0042-audit-log-scoped-to-login-and-the-bff-proxy.md), [0045](0045-silent-refresh-and-admin-session-revocation.md)).
- **Start-up ordering**: AuthFacade waits for OpenEMR's own readiness probe, and depends on the seed
  services that register its OAuth client and its own database completing first, so a `docker compose up`
  can never leave it running against a stale registration or an absent schema
  ([0036](0036-wait-for-openemrs-readiness-probe-at-startup.md), [0037](0037-authfacade-depends-on-seed-completing.md)).

### What was removed, and why that matters

AuthFacade did not start as just a BFF. Early on it was a general-purpose token broker exposing its own
`/connect/token` with a password grant ([0001](0001-authfacade-as-token-broker.md),
[0003](0003-delegate-credential-verification-to-openemr.md)) — a deliberate stand-in for the browser flow
before that flow existed. Once the browser flow existed end to end, nothing in the running system still
called the password grant, and keeping it "for a future mobile or M2M client" was considered and rejected:
a native app needs the same authorization-code-with-PKCE flow already built, and no machine-to-machine
need exists anywhere in ROADMAP's current scope. Removing it also removed bearer authentication outright
— the only thing that ever presented one — and three entire layered projects that existed only to serve a
flow that no longer ran ([0035](0035-password-grant-and-bearer-auth-are-removed.md)). The solution shrank
from six AuthFacade projects to two. This is not a footnote: it is why the design above is as small as it
reads. A bridge with one way in is easier to secure than one with two, even if the second way was once the
only one that worked.

## Consequences

- **M1 is done.** Every security item ROADMAP.md tracked under it is either built (the bullets above) or
  recorded below as an accepted risk. This ADR, merged, is the stated exit criterion.
- Future milestones build on this bridge as it stands today. M3 (the gateway validating tokens of its own)
  and the domain services (Patient Management, Clinical Care) inherit everything above without needing to
  re-decide it — and inherit its accepted risks too, until one of them actually needs the gap closed.
- **Accepted risks — deliberately not fixed, not forgotten:**
  - *Authorization for OpenEMR data is not built.* `/api/v1/me` confirms identity only. Nothing in the
    bridge decides what a signed-in subject may do with OpenEMR's data; that decision does not exist yet,
    for anyone.
  - *Admin-initiated revocation is allowlist-only, not a real authorization model.* `Admin:Subjects` is a
    fixed list, not a role or claim OpenEMR issues — the same missing-authorization-model gap as above,
    scoped narrowly to one endpoint rather than solved generally ([0045](0045-silent-refresh-and-admin-session-revocation.md)).
  - *Nothing here can reach OpenEMR's own side of a token.* OpenEMR exposes no server-to-server
    revocation endpoint (confirmed against its own discovery document and source) — a refreshed or
    revoked AuthFacade session has no bearing on whether the underlying OpenEMR refresh token still works
    outside AuthFacade, until it naturally expires ([0044](0044-session-store-backs-the-cookie-for-revocation.md),
    [0045](0045-silent-refresh-and-admin-session-revocation.md)).
  - *Running more than one AuthFacade replica is untested.* The three structural reasons it couldn't work
    are gone ([0043](0043-authfacade-session-state-in-a-dedicated-mariadb-database.md),
    [0046](0046-persisted-openiddict-certificates.md)), but removing a blocker is not the same claim as
    proving the result — nobody has run two replicas against this design yet.
  - *The browser login's OIDC scope carries no `api:oemr`*, so the BFF proxy's calls are rejected by
    OpenEMR itself until that scope is deliberately widened — not done here, since nothing downstream
    needs OpenEMR's protected API yet ([0029](0029-yarp-proxies-browser-sessions-to-openemrs-api.md)).
  - *A failed silent refresh produces no re-authentication prompt.* The BFF proxy falls back to the stale
    token and relays OpenEMR's own 401 unchanged; deciding what the dashboard does in response to that 401
    is outside the bridge's own scope ([0045](0045-silent-refresh-and-admin-session-revocation.md)).
  - *The seed's own OAuth client registration is partial.* It updates scope on an existing row but not
    `grant_types`, so changing those needs a manual edit or a fresh volume ([0005](0005-oauth-client-provisioning-and-scopes.md)).
  - *An upstream OpenEMR defect* returns 500, not a clean rejection, on some wrong-password attempts, when
    its own failed-login counter receives an object where it expects a string. AuthFacade cannot prevent
    this from outside; there is no automated regression guard for it today, since the one that existed was
    removed with the password grant it tested ([0035](0035-password-grant-and-bearer-auth-are-removed.md)).
- 81 unit tests, 1 architecture test, and 5 live integration tests pass as of this ADR — the architecture
  test enforcing the one layering rule still meaningful with two projects left: `Composition` never
  references `Api` or ASP.NET Core.

## Alternatives considered

- **No capstone at all — let the ADR index be the summary.** Rejected: the index is a table of titles and
  dates; it answers "what decisions exist," not "what does the finished thing do and guarantee." Someone
  arriving fresh needs the second question answered before the first is useful.
- **Write this earlier, as a living document updated alongside each decision.** Rejected: it would have
  been wrong at every intermediate point (the design kept changing — a password grant existed, then
  didn't; sessions were unrevocable, then were) and would have invited editing past ADRs to match it,
  against this project's own rule that a decision is never rewritten to change its outcome. A capstone
  written once, at the end of the milestone it closes, is both cheaper and more honest than maintaining a
  moving target throughout.

## Related decisions

Synthesizes, in order: [0001](0001-authfacade-as-token-broker.md), [0003](0003-delegate-credential-verification-to-openemr.md),
[0005](0005-oauth-client-provisioning-and-scopes.md), [0024](0024-browser-authorization-code-flow-with-pkce.md),
[0026](0026-openiddict-client-drives-the-authorization-code-flow.md), [0027](0027-persist-tokens-in-the-session-cookie.md),
[0028](0028-browser-login-and-session-assume-a-single-instance.md), [0029](0029-yarp-proxies-browser-sessions-to-openemrs-api.md),
[0030](0030-bff-proxy-forwards-to-the-gateway-not-each-service.md), [0033](0033-gateway-fronts-webapp-and-authfacade.md),
[0034](0034-gateway-preserves-the-original-host-header.md), [0035](0035-password-grant-and-bearer-auth-are-removed.md),
[0036](0036-wait-for-openemrs-readiness-probe-at-startup.md), [0037](0037-authfacade-depends-on-seed-completing.md),
[0038](0038-rate-limit-login-and-callback-per-client-address.md), [0039](0039-gateway-terminates-tls-authfacade-trusts-forwarded-headers.md),
[0040](0040-rp-initiated-logout.md), [0041](0041-circuit-breakers-guard-the-remaining-openemr-calls.md),
[0042](0042-audit-log-scoped-to-login-and-the-bff-proxy.md), [0043](0043-authfacade-session-state-in-a-dedicated-mariadb-database.md),
[0044](0044-session-store-backs-the-cookie-for-revocation.md), [0045](0045-silent-refresh-and-admin-session-revocation.md),
[0046](0046-persisted-openiddict-certificates.md). Closes ROADMAP.md's M1.
