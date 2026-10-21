# Changelog 2026-10-21

## Summary

Restructured AuthFacade into layered projects, loosened the login credential rules so existing OpenEMR accounts are not rejected locally, made malformed OpenEMR responses return 502, and added unit, architecture and live integration tests. The decisions are recorded in ADRs 0007 to 0009, and the earlier ones were converted from the 10-10 and 10-11 changelogs into ADRs 0001 to 0006 under `docs/architecture/`.

## Added

- **Layered AuthFacade projects** under `src/Services/AuthFacade/`:
  - `OpenEmrNext.AuthFacade.Domain`: value objects `Username`, `Password`, `GrantType`.
  - `OpenEmrNext.AuthFacade.Application`: ports (`IOpenEmrIdentityProvider`, `IAccessTokenIssuer`), `PasswordLoginHandler`, and `AddApplication`.
  - `OpenEmrNext.AuthFacade.Infrastructure`: `OpenEmrIdentityProvider` (OpenEMR client), `JwtAccessTokenIssuer`, and options classes.
  - `OpenEmrNext.AuthFacade.Api`: endpoints and `AddAuthFacadeApi` / `UseAuthFacadeApi` extension methods. `Program.cs` only calls these.
- **Test projects** under `tests/Services/AuthFacade/`:
  - `OpenEmrNext.AuthFacade.UnitTests` (71 tests): value objects, the login handler, the OpenEMR client with a fake HTTP handler, the JWT issuer, startup validation, and endpoints run in-process on a random local port.
  - `OpenEmrNext.AuthFacade.ArchitectureTests` (6 tests): layer dependency rules, checked from each assembly's references.
  - `OpenEmrNext.AuthFacade.IntegrationTests` (13 tests): run against the live stack. Added wrong-password passthrough, short legacy username, oversized password, and malformed `/me` token.
- **`docs/architecture/`**: ADRs 0001 to 0009 and an index.
- **`Password.TakeValue()`**: returns the plaintext once and clears its buffer. Later calls throw.
- **502 response** from `/connect/token` when OpenEMR returns 200 without a usable token.

## Changed

- **Login credential rules are loosened.** Username accepts 1 to 255 characters with no control characters, matching `users.username`. Password accepts 1 to 1024 characters. The complexity policy (8 to 128 characters, mixed classes) that was briefly in the code is removed; it would have rejected existing accounts. See ADR 0008.
- **`GrantType` validates the grant on `/connect/token`** through `TryParse`, alongside `Username` and `Password`.
- **Configuration is bound and validated at startup.** Missing `OpenEmr:BaseUrl`, `OpenEmr:ClientId`, `OpenEmr:ClientSecret` or `Jwt:Key` now stops the host. Previously the first two failed on the first request. `Jwt:Issuer`, `Jwt:Audience` and `Jwt:LifetimeMinutes` are configurable, with the old values as defaults.
- **Compose and Docker.** `docker-compose.yml` builds from `src/Services/AuthFacade` with the `Dockerfile` there. The image restores all four projects and runs `OpenEmrNext.AuthFacade.Api.dll`.
- **`OpenEMR-Next.slnx`** lists the new projects under `/src/AuthFacade/` and `/tests/`.
- **`OpenEmr.AuthFacade.Api.http`** (now `OpenEmrNext.AuthFacade.Api.http`) calls the real endpoints. It previously called a template `/weatherforecast` route.
- **README and ROADMAP** point to the new paths and test project names. Dated changelogs are left as written.

## Removed

- **`src/AuthFacade.Service/`** and **`tests/AuthFacade.Service.Integration.Tests/`**, replaced by the layered projects above.
- **`Clients/OpenEmrAuthClient.cs`**, replaced by `OpenEmrIdentityProvider`.
- **`Microsoft.AspNetCore.OpenApi`** package, unused by the service.

## Fixed

- **Malformed OpenEMR success responses returned 500.** A 200 response without `access_token`, with a non-JSON body, or with an unreadable token now returns 502 `server_error`.
- **`/me` tests asserted the wrong property names.** The response uses camelCase (`subject`, `name`).

## Verified

- The solution builds with 0 warnings. `dotnet format --verify-no-changes` passes for the service and test projects.
- Full Release test run: 71 unit, 6 architecture and 13 integration tests, all passing. The integration run used the Docker stack restored from the CI snapshot, with the AuthFacade container rebuilt from this code.
- The AuthFacade image builds with the new Dockerfile. `docker compose config` validates with `.env.example`.

## Findings

- **OpenEMR returns 400 `invalid_grant` for a wrong password, not 401.** AuthFacade passes this through unchanged. Earlier assumptions about 401 were wrong, and the integration test now asserts 400.
- **Short legacy usernames reach OpenEMR.** A username of `a` with password `x` gets OpenEMR's `invalid_grant`, not a local `invalid_request`.

## Known limitations and follow-ups

- **Password copies remain in memory.** The form string and the HTTP request string are immutable .NET strings and cannot be zeroed. `TakeValue()` limits how long the plaintext lives; it does not remove every copy. See ADR 0009.
- **Complexity policy is not enforced anywhere.** It belongs on a separate value object used when accounts are created or passwords are changed. No such flow exists yet.
- **OpenEMR's 5xx responses pass through.** A 500 from OpenEMR is returned to the client as a 500 with OpenEMR's body. Only malformed success responses are mapped to 502.
- **Seed does not reconcile `grant_types`.** See ADR 0005.
- **Architecture rules use reflection.** The ROADMAP names ArchUnitNET. See ADR 0007.
- **Branch name.** The work branch is `refactor/authfacade-layered-architecture`. It was first named `feat/authenticaion`, which had a typo and an unlisted prefix.
- **Decision pending: `OpenEmr.Data` namespace** still uses the `OpenEmr` prefix, while the AuthFacade projects use `OpenEmrNext`.
- **ADRs 0007 to 0009 are Proposed** and need review before they are accepted.
