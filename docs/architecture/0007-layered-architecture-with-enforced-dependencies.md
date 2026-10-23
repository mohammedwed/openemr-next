# 0007. AuthFacade is split into layered projects with enforced dependency rules

- **Status:** Proposed
- **Date:** 2026-10-21

## Context

AuthFacade began as one project with folders named by layer. Nothing stopped a domain type from referencing ASP.NET, or the application from calling OpenEMR directly. `docs/domain-driven-design.md` sets the layering rules, and `docs/openemr-ddd-design.md` §7 sets the project layout.

## Decision

The service is four projects under `src/Services/AuthFacade/`, named `OpenEmrNext.AuthFacade.<Layer>`:

| Project | Contains | May reference |
|---|---|---|
| `Domain` | Value objects | base class library only |
| `Application` | Ports, the login handler, `AddApplication` | `Domain`, DI abstractions |
| `Infrastructure` | OpenEMR client, JWT issuer and validation | `Application` |
| `Api` | Endpoints, `Program.cs`, composition | `Application`, `Infrastructure` |

> **Note (2026-10-23):** ADR 0010 moves the ports from `Application` into a new `Application.Ports` project. The HTTP shapes live in the API project. The table above is the layout before that change.

Rules are enforced by `OpenEmrNext.AuthFacade.ArchitectureTests`, which reads each assembly's referenced assemblies and fails the build on a violation. The roadmap names ArchUnitNET. This project uses reflection instead, to avoid a new package. The rules can move to ArchUnitNET later without changing what they check.

## Consequences

- Domain and application code cannot reach HTTP, the database or OpenEMR without the build failing.
- The API wiring is an extension method, so in-process tests can host the real pipeline with a fake identity provider.
- Adding a second adapter, such as a different identity provider, means a new Infrastructure implementation of an existing port.

## Alternatives considered

- **One project with folders.** Cheaper, but nothing enforces the rules.
- **ArchUnitNET from the start.** Richer rule language, but it adds a package the restore cache did not have. It remains the likely replacement.
