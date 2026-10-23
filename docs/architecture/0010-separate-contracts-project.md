# 0010. AuthFacade HTTP contracts live in the API project; ports live in their own project

- **Status:** Accepted
- **Date:** 2026-10-23

## Context

AuthFacade's request and response shapes were anonymous objects written inline in the endpoints, so they had no named types. The application's ports (`IOpenEmrIdentityProvider`, `IAccessTokenIssuer`) and their result types were in the Application project, which ADR 0007 specifies. The HTTP shapes and the ports have different audiences, so the ports are separated. The HTTP shapes were first given a project of their own, but no client outside the API references them, so they live with the API.

## Decision

- **`OpenEmrNext.AuthFacade.Application.Ports`**, a new project under `src/Services/AuthFacade/`, holds the application ports, with their result records and exceptions. It references the Domain project, because the port signatures use `Username` and `Password`. Those value objects stay in Domain and are not copied.
- **`Api/Contracts/`**, a folder in the API project, holds the public HTTP shapes: token, error, `/me` and `/hello` response bodies, and the form field names accepted by `/connect/token`. Namespace `OpenEmrNext.AuthFacade.Api.Contracts.*`.

Dependency rules, enforced by `OpenEmrNext.AuthFacade.ArchitectureTests`:

- Domain references neither Ports nor the API.
- Ports reference only Domain and the base class library.
- Application references Domain, Ports, and DI abstractions.
- Infrastructure references Ports and implements them. It does not reference Application or the API.

Serialized names are pinned by `ContractSerializationTests`. The wire format must not change without a new ADR that supersedes this one.

This changes the layout in ADR 0007: ports move from Application to their own project. ADR 0007 is not edited; see its note.

## Consequences

- The ports can be reviewed separately from the HTTP shapes, and Infrastructure depends only on Ports.
- The HTTP shapes live beside the endpoints that return them, so a change to a field name is in the same project as the endpoint.
- A client that needs the HTTP shapes would have to depend on the API assembly. If that becomes necessary, the folder can be extracted into its own project; the namespaces would have to change at that point.

## Alternatives considered

- **A separate project for the HTTP shapes.** It was built and then reverted, because no client references the shapes outside the API.
- **Keep the ports in Application**, as ADR 0007 specifies. This is still valid, and the most consistent with ADR 0007.
- **Move the value objects into Ports.** Rejected because it would copy domain types into a second project.
