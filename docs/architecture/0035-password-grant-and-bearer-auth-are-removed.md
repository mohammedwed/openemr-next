# 0035. The password grant and bearer authentication are removed

- **Status:** Accepted
- **Date:** 2026-11-01
- **Supersedes:** [0001](0001-authfacade-as-token-broker.md), [0003](0003-delegate-credential-verification-to-openemr.md), [0007](0007-layered-architecture-with-enforced-dependencies.md), [0008](0008-login-credential-rules.md), [0009](0009-password-plaintext-retrieved-once.md), [0011](0011-host-neutral-composition-root.md), [0013](0013-pass-openemr-tokens-through.md), [0014](0014-password-grant-does-not-check-client-secret.md), [0015](0015-development-certificate-bypass-only-in-development.md), [0016](0016-facade-token-response-shape.md), [0017](0017-circuit-breakers-guard-openemr-calls.md), [0018](0018-rate-limit-the-login-endpoint-per-client-address.md), [0019](0019-token-endpoint-binds-typed-form-parameters.md), [0021](0021-token-form-is-a-request-dto-with-attribute-validation.md), [0022](0022-binder-failures-return-a-bare-400.md), [0023](0023-token-endpoint-accepts-a-refresh-grant.md)
- **Builds on:** [0026](0026-openiddict-client-drives-the-authorization-code-flow.md)

## Context

ADR 0001 named the password grant a stand-in: "deprecated in OAuth 2.1, kept only for this integration,"
with "the production target" being the browser authorization-code flow with PKCE, which was not implemented
at the time. ADR 0024 repeated the same framing when that flow was first built. That target is now built,
working end to end through the gateway (ADR 0026, ADR 0033, ADR 0034) — the stand-in's reason to exist is
gone.

Nothing in the running system still used the password grant. `WebApp` and `LoginUi` only ever drive
`/oidc/login`'s browser flow; `POST /api/v1/connect/token` had no caller left except its own tests. Two
motivations for keeping it anyway were considered and rejected:

- **A future mobile client.** Rejected: a native app authenticating a human is exactly the authorization-code
  with PKCE flow already built, launched through the system browser instead of a page redirect — not a
  reason to keep a grant that hands a password directly to the client.
- **A future machine-to-machine caller.** Rejected: nothing in ROADMAP.md's current scope (M0–M6) needs one.
  The only place M2M is even mentioned is Phase 7 ("Data ownership transition... sync"), explicitly *not
  scheduled for the current scope*. If that need materializes later, it gets its own ADR against whatever
  OpenEMR actually supports then (`client_credentials` needs a JWKS registration AuthFacade does not have —
  see ADR 0014's deferred alternative) — not a standing justification for keeping a deprecated grant.

Removing the grant also removes its only caller of bearer authentication (`OpenEmrBearerHandler`,
`IExternalTokenVerifier`): nothing else in the system ever issues or presents a raw bearer token: the
browser/dashboard path has always been the session cookie (ADR 0026/0027), and the BFF proxy attaches the
real OpenEMR token server-side (ADR 0029).

Pulling those two threads out left three of AuthFacade's five layered projects (ADR 0007) with zero source
files: `OpenEmrNext.AuthFacade.Domain` (only `Username`/`Password`/`RefreshToken`/`GrantType`),
`OpenEmrNext.AuthFacade.Application.Ports` (only `IIdentityProvider`/`IExternalTokenVerifier`), and
`OpenEmrNext.AuthFacade.Application` (only `TokenGrantHandler`/`LoginResult`). There was no remaining
host-neutral business logic for ADR 0011's composition root to protect: everything left (the OpenIddict
browser flow, the BFF proxy) is ASP.NET-Core-specific by nature and already lived in the Api project.

## Decision

1. **Remove `POST /api/v1/connect/token`** and everything that existed only to serve it: `TokenEndpoints`,
   `PublicTokenEndpointExtensions`, `TokenRequest`/`TokenRequestFields`/`TokenResponse`, the `RateLimiting/`
   folder (ADR 0018's login limiter), `RequestValidationExtensions`, and the `ThrowOnBadRequest = false`
   configuration (ADR 0022) — nothing left binds a typed form.
2. **Remove bearer authentication**: `OpenEmrBearerHandler`, `BearerAuthenticationServiceCollectionExtensions`.
   The session-cookie scheme (ADR 0026) becomes the API's only `DefaultScheme`; `RequireAuthorization()` with
   no explicit policy now resolves to it directly instead of to a two-scheme policy.
3. **Delete `OpenEmrNext.AuthFacade.Domain`, `.Application.Ports`, and `.Application`** outright rather than
   leaving them as empty skeletons: there is no portable business logic left to isolate, so an empty layer
   would be scaffolding with nothing to protect. `OpenEmrNext.AuthFacade.Composition` and
   `OpenEmrNext.AuthFacade.Infrastructure` survive, shrunk to what is still real: `Infrastructure` now holds
   only `OpenEmrOptions`; `Composition`'s `AddAuthFacade` now only binds and validates that section directly,
   with no layer entry points left to delegate to.
4. **Remove the circuit breakers** (`CircuitBreakingIdentityProvider`, `CircuitBreakingTokenVerifier`,
   `CircuitBreakerPipeline`, `CircuitBreakerOptions`) rather than leaving them wired to nothing. The Polly
   *pattern* is sound and reusable — the decorators were just pointed at the one call graph that is gone.
   Reapplying it to the BFF proxy's or OpenIddict's own outbound calls, which currently have none, is a
   deliberately separate follow-up, not bundled into this removal.
5. **Remove `OpenEmrOptions.Scope` and `OpenEmrOptions.AcceptAnyServerCertificate`** (ADR 0015): both were
   read only by the deleted identity-provider/token-verifier `HttpClient`s. Neither OpenIddict's own JWKS
   fetch nor the BFF proxy's YARP `HttpClient` ever read `AcceptAnyServerCertificate` — removing it changes
   no runtime behavior, only removes a setting nothing honored for those paths.
6. **Regenerate the committed OpenAPI snapshot** (ADR 0031) and drop the Polly v7/v8 compatibility shim from
   `OpenEmrNext.AuthFacade.Api.csproj`: that shim existed only because `CircuitBreakerPipeline.cs` pulled
   Polly.Core v8 into the same compile graph as OpenIddict.Client.SystemNetHttp's transitive Polly v7
   dependency; with `CircuitBreakerPipeline.cs` gone, the ambiguity it resolved no longer exists, confirmed by
   building and running the OpenIddict browser-flow tests without it.

## Consequences

- AuthFacade is no longer a general-purpose "token broker for any client" (ADR 0001's original framing). It
  is a BFF for its own browser/dashboard clients, full stop: `GET /oidc/login` establishes a session cookie,
  the BFF proxy uses it server-side, and nothing else authenticates against it.
- The solution shrinks from six AuthFacade projects to three: `Infrastructure`, `Composition`, `Api`. The
  layered-architecture rules `LayerDependencyTests` enforces shrink to match: only the rules about
  `Infrastructure`/`Composition`/`Api` survive.
- `AuthFacadeStackTests` (live integration smoke tests) is unaffected — it never used the password grant.
  `MeEndpointTests` and `TokenEndpointTests` (live integration tests that did) are deleted outright: there is
  currently no live, non-unit-test coverage of `/api/v1/me` against the real stack. The in-process
  `OpenIddictClientCallbackTests` already covers `/me` through the cookie flow against a fake OpenEMR server;
  a live equivalent would need to drive the real OpenEMR login form the way this session's manual `curl`
  verification did, which is a larger, separate piece of work.
- A future mobile or M2M client is not blocked by this: the former gets the same authorization-code flow
  already built, aimed at a mobile-specific redirect URI; the latter gets its own ADR if and when OpenEMR
  actually needs to issue a token with no human or browser present.

## Alternatives considered

- **Keep the password grant for a future mobile client.** Rejected — see Context.
- **Keep it for a future M2M caller.** Rejected — see Context.
- **Leave `Domain`/`Application`/`Application.Ports` as empty projects**, in case a later feature needs a
  host-neutral layer again. Rejected: an empty project is scaffolding with nothing to protect, and a layer
  recreated for a real future need can be designed around that need, not around this one's leftovers.
- **Leave the circuit breaker decorators wired to nothing**, deferring their removal. Rejected: dead
  decorators referencing deleted interfaces are not a safe intermediate state, and the underlying pipeline is
  simple enough to reapply later without needing to preserve the old wiring now.

## Related decisions

Supersedes the ADRs listed above; each one's own status line now points here. Builds on ADR 0026's
session-cookie scheme, which becomes the API's sole authentication scheme as a result. Leaves ADR 0017's
underlying Polly pattern available for a future ADR that reapplies it to the BFF proxy or the OpenIddict
client's own outbound calls.
