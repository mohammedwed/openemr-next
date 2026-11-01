# Changelog 2026-11-01 (continued): password grant and bearer auth removed

## Summary

ADR 0001 always called the password grant a stand-in for the browser authorization-code flow it hadn't
built yet. That flow has existed and worked end to end since ADR 0026/0033/0034 — the stand-in's reason to
exist was gone, and nothing in the running system (`WebApp`, `LoginUi`) used it anymore. Removed it, removed
the bearer authentication it was the only source of, and removed the three layered projects that held
nothing else. See ADR 0035.

## Removed

- **`POST /api/v1/connect/token`** and everything that existed only to serve it: `TokenEndpoints`,
  `PublicTokenEndpointExtensions`, `RequestValidationExtensions`, the `TokenRequest`/`TokenRequestFields`/
  `TokenResponse`/`ErrorResponse` contracts, and the per-client-address rate limiter (ADR 0018).
- **Bearer authentication**: `OpenEmrBearerHandler` and its registration. The session cookie (ADR 0026) is
  now the API's only `DefaultScheme`.
- **`OpenEmrNext.AuthFacade.Domain`, `.Application.Ports`, and `.Application`**, outright, not left as empty
  skeletons: each held only types the removal above made unreachable
  (`Username`/`Password`/`RefreshToken`/`GrantType`; `IIdentityProvider`/`IExternalTokenVerifier`;
  `TokenGrantHandler`/`LoginResult`). There is no host-neutral business logic left for ADR 0011's composition
  root to protect — everything remaining (the OpenIddict browser flow, the BFF proxy) is ASP.NET-Core-specific
  by nature.
- **The circuit breakers** (`CircuitBreakingIdentityProvider`, `CircuitBreakingTokenVerifier`,
  `CircuitBreakerPipeline`, `CircuitBreakerOptions`) guarding the now-deleted calls. The Polly pattern itself
  is sound and portable; reapplying it to the BFF proxy's or OpenIddict's outbound calls — which have none
  today — is a deliberately separate follow-up.
- **`OpenEmrOptions.Scope` and `AcceptAnyServerCertificate`** (ADR 0015): both were read only by the deleted
  `HttpClient`s. Neither the OpenIddict client's JWKS fetch nor the BFF proxy's YARP `HttpClient` ever read
  `AcceptAnyServerCertificate`, so this changes no runtime behavior.
- **The Polly v7/v8 compile-time workaround** in `OpenEmrNext.AuthFacade.Api.csproj`: it existed only because
  `CircuitBreakerPipeline.cs` pulled Polly.Core v8 into the same compile graph as OpenIddict.Client's
  transitive Polly v7 dependency. With that file gone, the ambiguity is gone too — confirmed by building and
  running the OpenIddict browser-flow tests without the workaround.

## Considered and rejected

- **Keeping it for a future mobile client.** A native app authenticating a human needs the same
  authorization-code-with-PKCE flow already built, launched through the system browser — not a grant that
  hands the app a raw password.
- **Keeping it for a future machine-to-machine caller.** Nothing in ROADMAP.md's current scope needs one;
  the only mention is Phase 7, explicitly not scheduled. OpenEMR's `client_credentials` grant would also need
  a JWKS registration AuthFacade doesn't have (ADR 0014) — a real need gets its own ADR against whatever
  OpenEMR actually supports then.

## Changed

- The solution shrinks from six AuthFacade projects to two: `Composition` (now holds `OpenEmrOptions`
  directly and binds/validates it — no layer entry points left to delegate to) and `Api`. `Infrastructure`
  briefly survived holding just that one config class, then was deleted too: a project with one file isn't
  worth the project.
- `LayerDependencyTests` shrinks to match: the one rule left worth enforcing is that `Composition` never
  references `Api` or ASP.NET Core.
- The committed OpenAPI snapshot (ADR 0031) is regenerated; the generated `AuthFacadeClient` loses
  `IssueTokenAsync` and keeps only `GetCurrentUserAsync`.
- `appsettings.json`/`appsettings.Development.json`, `docker-compose.yml`, and `docs/configuration.md` lose
  the `LoginRateLimit`, `OpenEmr:Scope`, and `OpenEmr:AcceptAnyServerCertificate` settings.

## Tests

- Unit tests: 32 pass (down from 240 — the removed surface's own tests, plus the password-grant-specific
  cases trimmed from `ApiEndpointTests`, `OpenApiDocumentTests`, `ContractSerializationTests` and
  `EndpointNameTests`). Architecture tests: 5 pass (down from 12, for the same reason).
- `AuthFacadeStackTests` (live) is unaffected — it never used the password grant. `MeEndpointTests` and
  `TokenEndpointTests` (live) are deleted: there is now no live, non-unit-test coverage of `/api/v1/me`
  against the real stack. `OpenIddictClientCallbackTests` already covers it through the cookie flow against a
  fake OpenEMR server; a live equivalent would need to drive the real OpenEMR login form, a larger, separate
  piece of work.
- Verified against the real stack: rebuilt and restarted `auth-facade`, confirmed `/health` (200),
  `/api/v1/connect/token` (404, gone), `/api/v1/me` with no token (401), `/oidc/login` through the gateway
  (302 to the real OpenEMR `authorize` endpoint with the correct `redirect_uri`), and the BFF proxy with no
  session (401, relayed from OpenEMR, not a crash).
