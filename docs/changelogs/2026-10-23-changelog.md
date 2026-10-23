# Changelog 2026-10-23

## Summary

Added a host-neutral composition root, moved configuration reading into it, and added RFC 8693 token exchange so that AuthFacade accepts OpenEMR-issued JWTs. Added OpenAPI with the Scalar UI in Development. The decisions are recorded in ADRs 0010 to 0012, and ADRs 0007 to 0011 are now Accepted.

## Added

- **`OpenEmrNext.AuthFacade.Composition`** project. `AddAuthFacade(services, configuration)` is the single registration entry point every host calls. It binds and validates the `OpenEmr` and `Jwt` options. It references no ASP.NET package. See ADR 0011.
- **Token exchange (RFC 8693)** on `POST /connect/token`, using `grant_type=urn:ietf:params:oauth:grant-type:token-exchange`, `subject_token` and `subject_token_type=urn:ietf:params:oauth:token-type:jwt`. A valid OpenEMR JWT returns an AuthFacade token for the same subject. Responses: 200 on success, 400 `invalid_grant` for a token that does not verify, 400 `invalid_request` for missing or wrong fields, and 503 `server_error` when OpenEMR's keys cannot be fetched. See ADR 0012.
  - `OpenEmrNext.AuthFacade.Application.Ports`: `IExternalTokenVerifier`, `VerifiedToken`, `TokenVerifierUnavailableException`.
  - `OpenEmrNext.AuthFacade.Application`: `TokenExchangeHandler` and its result types.
  - `OpenEmrNext.AuthFacade.Infrastructure`: `OpenEmrTokenVerifier`. It checks RS256 signatures against the keys at `{OpenEmr:BaseUrl}/oauth2/default/jwk` and caches them for an hour. It checks issuer, audience, lifetime and signature, and it refreshes the keys at most once every five minutes after a signature failure.
  - `OpenEmr:Issuer` option, required. It is set in `appsettings.Development.json` and in `docker-compose.yml` as `OpenEmr__Issuer`.
- **OpenAPI and Scalar** in Development only. `AddAuthFacadeHttpDependencies` registers `AddOpenApi()`, and `Program.cs` maps `/openapi/v1.json` and the Scalar UI at `/scalar/v1`. Packages: `Microsoft.AspNetCore.OpenApi` 10.0.12 and `Scalar.AspNetCore` 2.18.3.
- **`MapAuthFacadeEndpoints`** in the API project. It maps the system, token and user endpoint groups, so endpoint mapping is separate from middleware.
- **Consumption guide** at `src/Services/AuthFacade/OpenEmrNext.AuthFacade.Composition/README.md`. It covers how a new host references Composition, registers services, resolves the use case in a scope, parses inputs, and handles the four login outcomes.
- **Tests**:
  - Unit tests: `TokenExchangeHandlerTests`, `OpenEmrTokenVerifierTests` (algorithm confusion, wrong issuer, audience and key, expiry, malformed input, unreachable JWKS), and `TokenExchangeEndpointTests`.
  - Integration test: `TokenExchangeTests`, which exchanges a real OpenEMR token and checks that `/me` returns the same subject, and that an altered signature is rejected.

## Changed

- **Configuration is read only in the composition root.** `AddAuthFacade` binds and validates `OpenEmrOptions` and `JwtOptions`. `AddInfrastructure()` takes no configuration. `AddOpenEmrIdentityProvider`, `AddOpenEmrTokenVerifier` and `AddJwtTokenIssuance` read the bound options through `IOptions<T>`.
- **The development certificate bypass is configuration, not environment.** `OpenEmr:AcceptAnyServerCertificate` defaults to `false` and is set only in `appsettings.Development.json`. Infrastructure and Composition no longer take `IHostEnvironment`.
- **Packages moved.** `Microsoft.Extensions.Options.ConfigurationExtensions` and `Microsoft.Extensions.Options.DataAnnotations` moved from Infrastructure to Composition. `Microsoft.Extensions.Hosting.Abstractions` was removed from Infrastructure.
- **API extension methods renamed.** `AddAuthFacadeApi` became `AddAuthFacadeHttpDependencies`, and `UseAuthFacadeApi` became `UseAuthFacadeHttpMiddleware`. Bearer validation and OpenAPI registration live in the API project.
- **Identity port renamed** from `IOpenEmrIdentityProvider` to `IIdentityProvider`. The OpenEMR implementation keeps the name `OpenEmrIdentityProvider`. The test double is `FakeIdentityProvider`.
- **`GrantType`** accepts `password` and the token exchange grant.
- **ADR statuses.** ADRs 0007 to 0010 are Accepted, and ADR 0011 is Accepted.
- **The Docker AuthFacade image** restores the API and the five projects it references, including Composition.
- **Login path.** `TokenEndpoints` dispatches on the grant type. The password login and its responses are unchanged.

## Removed

- **`IHostEnvironment` parameters** from `AddInfrastructure`, `AddOpenEmrIdentityProvider` and `AddAuthFacade`.
- **The `Development`-environment check** that enabled the TLS bypass. The bypass is now the `OpenEmr:AcceptAnyServerCertificate` setting.

## Fixed

- **Obsolete API warning.** `ConfigureHttpMessageHandlerBuilder` was replaced with `ConfigurePrimaryHttpMessageHandler`, which takes the service provider.
- **Token claim mapping.** The verifier turns off inbound claim mapping, so `sub` is read as `sub` and not as the long `nameidentifier` URI.
- **Stale container.** The running AuthFacade container still used an image from before the OpenAPI change, so `/scalar/v1` returned 404. Rebuilding the `auth-facade` service fixed it.

## Verified

- The solution builds with 0 errors and 0 warnings.
- Unit tests: 100 passed. Architecture tests: 12 passed.
- Integration tests: 15 passed against the Docker stack, including the live token exchange and `/me` subject check.
- The AuthFacade container responds on `/hello`, `/openapi/v1.json` and `/scalar/v1` with 200.

## Findings

- **OpenEMR tokens in this stack** are RS256, with `iss` set to `https://localhost:9300/oauth2/default` and `aud` set to the OAuth client ID (`authfacade`). The `sub` is a UUID. The token carries no `name` or `preferred_username` claim, and its `id_token` carries none either.
- **OpenEMR granted only the `openid` scope** when the client asked for `openid api:oemr`. The exchange does not check scopes.
- **OpenEMR's userinfo endpoint returned nothing** for the token, so it can't supply a name in this stack.
- **Stack restart.** The `openemr-next` containers were absent when the integration tests were first run, so `docker compose up -d` recreated them. OpenEMR took about three minutes to become ready after a fresh start, which is longer than the readiness fixture's two-minute window. The first run failed for that reason, and the second run passed after the wait.

## Known limitations and follow-ups

- **Exchanged tokens have an empty `name`.** See ADR 0012.
- **No scope check on the exchange.** A token without `api:oemr` is accepted.
- **Readiness window is two minutes.** `ReadinessFixture` should wait longer, or the stack should be given time to start before the tests run.
- **Public API is not tracked.** `PublicApiAnalyzers` and XML documentation output have not been added to the layer projects.
- **ADR 0010 still names `IOpenEmrIdentityProvider`.** It is a historical record, and the README and the consumption guide use the current name.
- **OpenAPI and Scalar are Development only.** Exposing them elsewhere needs a separate decision.
- **Password copies remain in memory.** See ADR 0009.
- **The JWKS endpoint path is fixed to the `default` realm** in `OpenEmrTokenVerifier`.
