# 0026. OpenIddict.Client drives the browser authorization-code flow against OpenEMR

- **Status:** Accepted — verified against the real stack: a genuine browser login completed end to end,
  logged as `AuthenticationScheme: AuthFacadeSession signed in.`
- **Date:** 2026-10-30
- **Supersedes:** [0024](0024-browser-authorization-code-flow-with-pkce.md)

## Context

ADR 0024 hand-rolled the authorization-code flow with PKCE: its own `state`/`nonce`/PKCE cookie, its own
redirect building, its own `id_token` verification. It worked in unit tests against a fake identity provider,
but failed the first time it ran against the real stack with `Invalid encryption version prefix` from
OpenEMR's own `AuthCodeGrant` — a failure that, in hindsight, was unrelated to the hand-rolled code itself
(see ADR 0025) but was enough to prompt using a maintained library instead of continuing to hand-roll PKCE,
state and `id_token` validation.

OpenIddict.Client was chosen specifically as a *client* library: AuthFacade remains a thin broker in front of
OpenEMR's own, already-conformant OAuth2/OIDC server (ADR 0001, ADR 0012). OpenIddict *server* components,
which would make AuthFacade its own OIDC provider, were not considered — ADR 0001 already rejected OpenIddict
for exactly that reason (it needs EF Core stores in the OpenEMR database, which the project does not change),
and a second OIDC provider alongside OpenEMR's own would be redundant, not an improvement.

That EF Core requirement did not disappear by using OpenIddict as a client instead of a server: the
interactive authorization-code flow is stateful by design — OpenIddict.Client's own "state token" mechanism
needs a database to protect against CSRF and replay, confirmed against OpenIddict's own documentation and
every official interactive-client sample, all of which configure `AddCore().UseEntityFrameworkCore()`. This
was a deliberate, discussed trade-off (see Alternatives), not an oversight: a small SQLite database, scoped
only to OpenIddict's own state tokens, was accepted in exchange for a maintained, spec-correct implementation.

Getting this working against the real stack took seven rounds of fixes, each confirmed by a distinct,
real error from OpenEMR or OpenIddict itself:

1. `/oidc/login` built the authorize URL from `OpenEmr:BaseUrl` (container-internal) instead of
   `OpenEmr:Issuer` (public) — the same class of bug ADR 0012 already named, reproduced here because the
   fix lived in new code, not copied from the old.
2. OpenIddict refuses to challenge outside HTTPS by default (`DisableTransportSecurityRequirement` exists
   precisely for this).
3. OpenIddict's own correlation cookie defaults to `SameSite=None` with `Secure` forced on — unusable on
   this stack's plain HTTP (ADR 0020) even with step 2's fix applied, since a `None` cookie without `Secure`
   is rejected outright by the cookie itself, not by the challenge check.
4. OpenEMR's `authorization_code` token exchange failed client authentication: the seeded client secret was
   never actually encrypted the way OpenEMR's own `validateClient()` expects (ADR 0025 — a pre-existing bug
   this flow was simply the first thing to ever exercise).
5. The `id_token`'s signing key was never found: setting `OpenIddictClientRegistration.Configuration`
   directly bypasses OpenIddict's discovery pipeline entirely, so a `JsonWebKeySetUri` on it is never
   followed.
6. Populating `JsonWebKeySet` manually was still not enough: token validation reads the flat
   `Configuration.SigningKeys` list, which only OpenIddict's own discovery pipeline normally populates from
   the key set.
7. The issuer string used for `iss` validation had a trailing slash added for URL-building convenience,
   which no longer matched OpenEMR's actual `iss` claim.

Only after all seven were fixed did a real browser complete the flow.

An eighth issue surfaced afterward, caught by `OpenIddictClientCallbackTests` rather than by live testing: a
`/callback` whose grant OpenEMR rejects, or whose `id_token` nonce does not match, never reached
`HandleCallbackAsync`'s own failure branch at all. OpenIddict's pipeline was writing its own plain-text/JSON
error response directly and short-circuiting before `AuthenticateAsync` returned control to the endpoint —
exactly the `"The response was successfully returned as a plain-text document: {error: ...}"` log lines seen
throughout live debugging, which had looked like routine diagnostic noise at the time. `EnableErrorPassthrough()`
(alongside the existing `EnableRedirectionEndpointPassthrough()`) fixes this: it hands the error to
`AuthenticateAsync` as an ordinary failed `AuthenticateResult` instead of writing a response itself, so the
generic `login_failed` redirect in Decision 6 actually runs for every failure path, not just some of them.

## Decision

1. **New packages, confined to the Api project**: `OpenIddict.Client`, `OpenIddict.Client.AspNetCore`,
   `OpenIddict.Client.SystemNetHttp`, `OpenIddict.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Sqlite`.
   `OpenIddict.Client.SystemNetHttp` transitively pulls `Microsoft.Extensions.Http.Polly`, which depends on
   the legacy `Polly` v7 package; its `Polly.CircuitBreaker.BrokenCircuitException` collides at compile time
   with the same type name in `Polly.Core` v8, which `CircuitBreakerPipeline.cs` already uses directly. Both
   `OpenEmrNext.AuthFacade.Api.csproj` and the unit test project exclude *compile* assets only
   (`ExcludeAssets="compile"`) for `Microsoft.Extensions.Http.Polly` and `Polly` — excluding `all` instead
   removes `Polly.dll` from the publish output entirely and throws `FileNotFoundException` at runtime,
   since `OpenIddict.Client.SystemNetHttp` genuinely loads it.
2. **`ClientDbContext`** (`Api/OpenIddictClient/ClientDbContext.cs`), backed by a SQLite file at a fresh,
   uniquely-named path per process (`Path.GetTempPath()` + a GUID), created with `EnsureCreatedAsync()` — no
   migration history, since the store holds nothing beyond one in-flight login's state tokens and losing it
   on restart is fine, the same reasoning ADR 0024 already applied to its Data Protection key ring. A fixed
   shared path would also let concurrent test hosts collide.
3. **A new cookie scheme, `AuthFacadeSession`**, holds the local session after a successful login
   (`HttpOnly`, `SameSite=Lax`, 12-hour lifetime). Registered via a second, additive `AddAuthentication()`
   call that does not set `DefaultScheme` — `/me` keeps resolving to the existing `Bearer` scheme with no
   explicit scheme named, unaffected.
4. **`OpenEmrConfigurationManager`** (`Api/OpenIddictClient/OpenEmrConfigurationManager.cs`) replaces the
   registration's `Configuration` property with a custom `IConfigurationManager<OpenIddictConfiguration>`,
   fetching and caching the JWKS itself (1-hour cache, `RequestRefresh()`-driven forced refresh) — the same
   cache-with-refresh shape `OpenEmrTokenVerifier` already uses for the equivalent bearer-token JWKS fetch.
   It also copies fetched keys into `Configuration.SigningKeys`, replicating the one step of OpenIddict's own
   discovery pipeline (`OpenIddictClientRetriever`) that setting a static `Configuration` skips.
5. **The registration's endpoints are built explicitly, not discovered** (`BuildRegistration` in
   `OpenIddictClientServiceCollectionExtensions.cs`): `AuthorizationEndpoint` from the publicly-reachable
   `OpenEmr:Issuer` (the browser follows it), `TokenEndpoint` and the JWKS URI from the container-internal
   `OpenEmr:BaseUrl` (AuthFacade calls them itself) — the same internal/public split ADR 0012 established,
   now applied a second time because OpenEMR's discovery document only ever publishes the single public host
   for every endpoint, which would silently reproduce the ADR 0012 bug class if left to automatic discovery.
   `Issuer` carries no trailing slash, matching OpenEMR's real `iss` claim exactly.
6. **Two endpoints** (`Api/Endpoints/OpenIddictClientEndpoints.cs`): `GET /oidc/login` resolves `returnUrl`
   against the existing allow-list and issues `Results.Challenge(...)` for the OpenIddict client scheme;
   `/callback` is OpenIddict's own redirection endpoint, reached via `EnableRedirectionEndpointPassthrough`.
   On success it copies every claim from OpenIddict's authenticated principal into a new identity and signs
   in with `AuthFacadeSession`; on failure (OpenIddict already validated the code exchange, the `id_token`,
   and its own state token's nonce — none of that is re-implemented) it redirects to the resolved return URL
   with a generic `?error=login_failed`, never OpenEMR's or OpenIddict's own error text. This depends on
   `EnableErrorPassthrough()` (see issue 8 above) — without it, OpenIddict answers a failed callback itself
   and `HandleCallbackAsync`'s failure branch never runs.
7. **`ReturnUrlValidator` and `BrowserFlowOptions` are reused from ADR 0024 as-is** — the open-redirect
   problem they solve does not depend on which engine drives the redirect. The rest of ADR 0024's hand-rolled
   pieces (`BrowserFlowEndpoints`, `OidcFlowCookieProtector`, `SessionCookieProtector`, `Pkce`,
   `BrowserFlowErrorCodes`) are not reused.
8. **`BrowserFlowEndpoints` is no longer mapped.** Both it and this ADR's endpoints would claim the same
   `/callback` registered with OpenEMR; only one can be live. Its ten tests
   (`BrowserFlowEndpointTests`) are marked `Skip`, not deleted, in case the hand-rolled flow is revisited —
   delete the file once that stops being a realistic possibility. `OidcFlowCookieProtector` and
   `SessionCookieProtector` stay registered in DI (harmless, unused) rather than being torn out for this
   change alone.
9. **Dev-only ephemeral certificates** (`AddDevelopmentEncryptionCertificate`/`AddDevelopmentSigningCertificate`)
   protect OpenIddict's own state tokens. A real deployment needs persisted ones; ADR 0020 already treats
   this stack as local-only, so this matches its existing maturity level.
10. **Wired into the Login UI.** `OpenEmrNext.LoginUi` went back to a static page — one link to
    `{AuthFacade:BaseUrl}/oidc/login`, the original design intent, now that this flow is proven. It replaced
    the self-contained password-grant form, which had no server-side call left once the button did not need
    one either, so the page dropped Blazor Server interactivity (`AddInteractiveServerComponents`, the
    circuit script, `AuthFacadeClient`) along with it. Its own `AuthFacade:BaseUrl` reverted to the
    host-published address (the browser follows the link directly), undoing the Compose-internal address the
    password-grant form's server-side call had needed. The page reads `/callback`'s `?error=` the same way
    ADR 0024's page did, but maps every value — `login_failed` and anything unrecognized alike — to one
    generic message, matching this ADR's single error code (point 6).

## Consequences

- A SQLite file now exists purely for OpenIddict's own state-token bookkeeping — the one deliberate
  reopening of ADR 0001/0007's "AuthFacade holds no identity data of its own" stance, scoped as narrowly as
  the library allows (see Alternatives).
- Client authentication now genuinely matters for this grant (ADR 0014, ADR 0025): a wrong or stale
  `OpenEmr:ClientSecret` breaks `/oidc/login` even though it does nothing for the password or refresh grants.
- `OpenEmrConfigurationManager` and `BuildRegistration` carry real, load-bearing knowledge of OpenIddict's
  internals (the `Configuration`-vs-`ConfigurationManager` split, the `JsonWebKeySet`-vs-`SigningKeys` split)
  that is not obvious from the public API surface and was only found by reading OpenIddict's own source;
  an OpenIddict upgrade that changes this internal shape could silently break JWKS resolution again.
- RP-initiated logout was not covered here, for the same reason ADR 0024 could not cover it: unconfirmed
  whether OpenEMR's discovery document publishes an `end_session_endpoint`. It does; [ADR 0040](0040-rp-initiated-logout.md) builds it.
- No shared Data Protection or OpenIddict-state key ring exists for multiple instances — the SQLite file and
  the dev certificates are both per-process.
- `/oidc/login` and `/callback` remain unrated by ADR 0018's per-address login rate limit, the same gap ADR
  0024 already left open.

## Alternatives considered

- **OpenIddict as AuthFacade's own OIDC server.** Rejected outright, not reconsidered here — ADR 0001's
  rejection (EF Core stores in the OpenEMR database) applies without needing new analysis, and a second OIDC
  provider alongside OpenEMR's own would be redundant regardless of the database question.
- **OpenIddict's documented "degraded mode"** (disabling the features that depend on `OpenIddict.Core`) as a
  way to avoid the database entirely. Investigated before deciding: the documentation frames it around
  disabling *server*-side client/reference-token validation, and it was not possible to confirm from the
  documentation alone whether the *client's* interactive-flow state-token requirement survives it. Not
  pursued further once a small SQLite database was accepted as an acceptable, narrowly-scoped trade-off.
- **Keep the hand-rolled flow (ADR 0024).** Rejected per the explicit decision to move to a library for
  OIDC-spec correctness. Its code and tests are not deleted (point 8), so this remains reversible.
- **Fetch and set `Configuration.JsonWebKeySet` once, synchronously, at registration time**, avoiding the
  need for a custom `IConfigurationManager`. Rejected: `BuildRegistration` runs during DI registration,
  before the host starts, and AuthFacade's existing design deliberately does not require OpenEMR to be
  reachable at startup (`auth-facade`'s own compose comment: calls to OpenEMR go through circuit breakers,
  not a startup check) — a blocking fetch there would contradict that.

## Related decisions

Implements the production target ADR 0001 named and ADR 0024 attempted. Reuses the internal/public endpoint
split ADR 0012 established, and hits the exact same class of bug ADR 0012 already fixed once, in new code
that did not inherit the old fix. Depends on ADR 0025 for the authorization-code grant's client
authentication to work at all. Does not reopen ADR 0002/0013's decision that AuthFacade issues no tokens of
its own — the `AuthFacadeSession` cookie carries a local identity derived from OpenEMR's claims, not a token
AuthFacade mints. Narrows, rather than reopens, ADR 0001/0007's "no identity data of its own" stance, to the
one SQLite file OpenIddict's own design requires.
