# 0011. Each host composes AuthFacade through a host-neutral composition root

- **Status:** Accepted
- **Date:** 2026-10-23

## Context

The service wiring lived in the API project. `AddAuthFacadeHttpDependencies` registers the application, the OpenEMR identity provider, JWT token issuance and bearer validation, and the API project is the only entry point. A second entry point, such as a worker or a different host, would have had to reference the API project, which pulls in the endpoints and the ASP.NET pipeline and reverses the dependency direction, or copy the wiring.

Infrastructure also depended on ASP.NET. The JWT registration added the ASP.NET `JwtBearer` scheme, the OpenEMR client used `StatusCodes` from `Microsoft.AspNetCore.Http`, and the project referenced the `Microsoft.AspNetCore.App` framework. A non-HTTP host would therefore load ASP.NET even though it needs none of it.

## Decision

1. **Each layer has one public registration entry point, named after the layer.** Application exposes `AddApplication()`. Infrastructure exposes `AddInfrastructure()`, which registers the OpenEMR identity provider and token issuance. It takes no configuration; it reads the bound `OpenEmrOptions` and `JwtOptions`. The specific registrations behind it are internal to Infrastructure, so a host cannot call them directly.
2. **`OpenEmrNext.AuthFacade.Composition`** is a new project that is the composition root for every host. It exposes `AddAuthFacade(IServiceCollection, IConfiguration)`, which binds and validates `OpenEmrOptions` and `JwtOptions` from configuration, then calls `AddApplication()` and `AddInfrastructure()`. Configuration is read only here. It references Application and Infrastructure. It references no ASP.NET package.
3. **Bearer validation is HTTP-specific.** The `JwtBearer` scheme moves to the API project, in `Api/Authentication`. Only the HTTP host registers it.
4. **Infrastructure does not reference ASP.NET.** The OpenEMR client uses its own status-code constant in place of `StatusCodes`. The JWT registration is named `AddJwtTokenIssuance`, since it no longer registers authentication.
5. **Each entry point is a host project.** A host references Composition, calls `AddAuthFacade`, and maps its own transport. It may reference Application to call use cases. It owns its transport contracts.

Dependency rules, enforced by `OpenEmrNext.AuthFacade.ArchitectureTests`:

- Domain, Ports, Application and Infrastructure do not reference Composition or the API.
- Composition does not reference the API or any `Microsoft.AspNetCore.*` assembly.
- Infrastructure does not reference any `Microsoft.AspNetCore.*` assembly.
- Only hosts reference Composition.

The public surface of AuthFacade is therefore: `AddAuthFacade` for registration, and the Application use cases, such as `PasswordLoginHandler`, for behavior. Each host decides what to expose over its transport.

This extends ADR 0007 and ADR 0010 with a composition project. ADR 0007 and ADR 0010 are not edited; see their notes.

## Consequences

- A new host references Composition and gets the same services without referencing the API or ASP.NET.
- Infrastructure can be built and tested without ASP.NET, and the architecture test fails if an ASP.NET reference returns.
- The API project gains the bearer registration, and it keeps the endpoints and the transport contracts.
- The solution has one more project and one more Dockerfile restore line.

## Alternatives considered

- **Keep the wiring in the API and have new hosts reference it.** Rejected, because it pulls in the endpoints and the ASP.NET pipeline and reverses the dependency direction.
- **Keep bearer validation in Infrastructure.** Rejected, because it keeps an ASP.NET dependency in the adapter layer. Non-HTTP hosts would still load ASP.NET.
- **Put the composition root in Infrastructure.** Rejected, because Infrastructure would then have to reference Application and the adapters, and it would no longer be an adapter layer.
