# Domain-Driven Design Guide

This document defines how OpenEMR Next will use Domain-Driven Design (DDD) to
discover, model, and protect business boundaries during modernization.

DDD is an approach for understanding the healthcare domain and expressing its
business rules in maintainable software. It is not a mandate to create a
microservice for every concept or database table.

The project will use **strategic DDD** to identify bounded contexts and
relationships, followed by **tactical DDD** inside selected contexts. A
bounded context may initially be implemented as a module, package, adapter, or
service. Deployment decisions will be based on measurable operational value.

## Goals

DDD in OpenEMR Next is intended to:

- Make business responsibilities explicit.
- Establish a shared language for clinical and technical stakeholders.
- Reduce accidental coupling between healthcare capabilities.
- Keep legacy database concerns out of new domain models.
- Make business rules independently testable.
- Support incremental migration without a big-bang rewrite.
- Provide evidence for deciding whether a context should become a service.

## Strategic design

### Business capabilities

The project will begin with capabilities and workflows rather than with
database tables or technical layers.

Initial capabilities for investigation include:

- Identity and authentication
- Authorization and access policy
- Patient administration
- Clinical documentation
- Scheduling
- Orders and results
- Medications and prescriptions
- Billing and claims
- Documents
- Notifications
- Interoperability and external integrations
- Reporting and analytics

These are discovery areas, not predetermined service boundaries. Each area
must be validated through workflow analysis, stakeholder input, dependency
analysis, and baseline measurements.

### Bounded contexts

A bounded context is a boundary within which a domain model and its language
have a specific, consistent meaning.

For each candidate context, document:

- Purpose and business responsibility
- Users, actors, and stakeholders
- Core workflows and decisions
- Commands and queries
- Entities and value objects
- Domain events
- Data owned by the context
- External dependencies
- What the context explicitly does not own
- Legacy tables involved during migration
- Security, audit, and continuity requirements

For example:

```text
Authentication owns:
- Credential verification
- MFA challenges
- Login throttling and lockout decisions
- Token issuance and revocation
- Authentication audit events

Authentication does not own:
- Clinical permissions
- Facility business rules
- Patient access policy
- Encounter authorization
- Clinical user-profile management
```

### Context map

The context map describes relationships and ownership between contexts:

```text
Identity and Authentication
        │ provides authenticated principal
        ▼
Authorization and Access Policy
        │ evaluates permissions and scope
        ▼
Clinical Documentation ───> Patient Administration
        │
        └── publishes clinical facts ──> Notifications / Reporting
```

During migration, new contexts may interact with legacy OpenEMR through an
anti-corruption layer:

```text
New bounded context
        │
        ▼
Legacy compatibility adapter
        │
        ▼
OpenEMR database and application behavior
```

The adapter translates legacy representations into the new domain model. New
domain code must not spread legacy table names, procedural conventions, or
unvalidated legacy assumptions throughout the application.

## Ubiquitous language

Each context will maintain a glossary of terms used by its stakeholders and
code. A term may have different meanings in different contexts; this is
preferable to forcing one ambiguous definition across the entire platform.

Initial examples:

| Term | Authentication context | Clinical context |
|---|---|---|
| User | An identity that can authenticate | May refer to a staff account |
| Principal | An authenticated actor represented in a token | The actor performing a clinical action |
| Facility | An identity or login scope | An organization or care location |
| Encounter | Usually not part of the authentication model | A clinical interaction with a patient |
| Role | An input to an authentication or authorization policy | A clinical or administrative responsibility |
| Session | A browser authentication state | May refer to a clinical workflow session |
| Token | A credential presented to an API | Not a substitute for clinical authorization |

The glossary must record:

- Definition
- Owning context
- Accepted synonyms
- Prohibited ambiguous usage
- Legacy equivalents
- Code representation
- API or event representation, where applicable

Legacy names such as `users`, `facility`, or `encounter` must not determine the
new model without domain validation. A legacy table may contain multiple
concepts or historical behavior that should be separated in the new design.

## Domain discovery process

The project will use the following process for each candidate context:

1. Identify stakeholders and business owners.
2. Map important workflows and variations.
3. Identify commands, decisions, and domain events.
4. Record rules, exceptions, and failure behavior.
5. Identify concepts that have a consistent meaning.
6. Separate concepts with conflicting meanings into contexts.
7. Map dependencies and integration relationships.
8. Identify data ownership and legacy compatibility requirements.
9. Define measurable outcomes and migration risks.
10. Select a small vertical slice for implementation.

Event storming, workflow mapping, domain interviews, repository analysis, and
production-safe metrics may all be used as discovery inputs. No single
technique is considered authoritative without validation by domain
stakeholders.

## Tactical design

Tactical DDD will be applied inside a bounded context to express business
rules. The following patterns are guidance rather than mandatory ceremony.

### Entities

Entities have identity and a lifecycle.

Potential authentication entities include:

- `AuthenticationAccount`
- `MfaEnrollment`
- `AccessToken`
- `ClientApplication`

An entity should represent a domain concept, not simply mirror a database row.

### Value objects

Value objects are immutable concepts defined by their values and validation
rules.

Potential value objects include:

- `UserId`
- `Username`
- `EmailAddress`
- `TokenId`
- `FacilityId`
- `AuthenticationMethod`

Example:

```csharp
public sealed record UserId(Guid Value);

public sealed record Username
{
    public string Value { get; }

    public Username(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Username is required.", nameof(value));
        }

        Value = value;
    }
}
```

Value objects should reject invalid values at construction and should not
require callers to repeat the same validation throughout the system.

### Aggregates

Aggregates define consistency boundaries and protect invariants.

For authentication, an aggregate may protect rules such as:

- An inactive account cannot authenticate.
- A locked account cannot begin a password flow.
- An MFA-required account cannot issue a fully authenticated token without
  successful MFA.
- A revoked token cannot be used.

Aggregates must not be created mechanically for every table. The boundary
should be based on business consistency and transaction requirements.

### Domain services

Domain services are appropriate for domain operations that do not naturally
belong to one entity or aggregate.

Examples include:

- `CredentialVerifier`
- `AuthenticationPolicy`
- `TokenIssuancePolicy`
- `MfaChallengePolicy`

Domain services should express business decisions, not HTTP handling,
database queries, or framework behavior.

### Domain events

Domain events represent meaningful facts that have occurred:

- `AuthenticationSucceeded`
- `AuthenticationFailed`
- `MfaChallengeRequired`
- `AccountLocked`
- `AccessTokenIssued`
- `AccessTokenRevoked`

Events should describe domain facts rather than implementation details such as
“a row was inserted.” Event payloads must have explicit ownership, versioning,
security, and privacy rules.

## Layering and dependency direction

A context should keep domain logic independent from delivery and persistence
technologies:

```text
API → Application → Domain
Infrastructure → Domain
```

A possible structure is:

```text
src/
└── Authentication/
    ├── Domain/
    │   ├── Entities/
    │   ├── ValueObjects/
    │   ├── Services/
    │   ├── Events/
    │   └── Repositories/
    ├── Application/
    │   ├── Commands/
    │   ├── Queries/
    │   └── Handlers/
    ├── Infrastructure/
    │   ├── LegacyOpenEmr/
    │   ├── Persistence/
    │   └── Cryptography/
    └── Api/
        ├── Endpoints/
        └── Contracts/
```

The domain layer must not depend directly on:

- ASP.NET controllers or HTTP
- Entity Framework implementation details
- Legacy table names
- Message-broker clients
- Configuration providers
- File or network storage

These dependencies belong at the application or infrastructure boundary.

## Legacy database integration

The legacy database is a migration dependency, not the domain model.

New domain code must access legacy data through domain-facing abstractions:

```csharp
public interface IAuthenticationAccountRepository
{
    Task<AuthenticationAccount?> FindByUsernameAsync(
        Username username,
        CancellationToken cancellationToken);
}
```

Infrastructure may provide an implementation such as:

```text
IAuthenticationAccountRepository
        └── LegacyOpenEmrAuthenticationAccountRepository
```

The adapter translates:

```text
legacy users row
→ AuthenticationAccount aggregate
```

For each legacy dependency, document:

- Tables read
- Tables written
- Query and update behavior
- Source of truth
- Data mapping and normalization
- Concurrency assumptions
- Failure behavior
- Audit requirements
- Migration and rollback procedure

Shared database access must have explicit ownership. Two applications must not
independently write the same authentication state without a documented
consistency and conflict strategy.

## Authentication and authorization

Authentication and authorization are separate domain concerns:

```text
Authentication:
- Who is this principal?
- How was identity verified?
- Is MFA required and complete?
- Which tokens are valid or revoked?

Authorization:
- May this principal perform this action?
- On which resource?
- In which facility, organization, or patient scope?
- Which policy and consent rules apply?
```

They may initially deploy together, but their models and responsibilities
should remain distinct. Authorization decisions and volatile permissions should
not be embedded permanently in long-lived identity tokens.

## Module versus service decisions

DDD boundaries and deployment boundaries are related but not identical. After a
context is modeled, decide whether it should be:

- A module in a modular monolith
- An independently tested package
- A separately deployable service
- A background worker
- A read model or reporting pipeline

Independent deployment is justified only when the context has:

- A stable and understood contract
- Clear data ownership
- Limited synchronous coupling
- Independent scaling or reliability needs
- Separate operational ownership
- A meaningful security or integration boundary
- A tested rollback path

If these conditions are not present, keep the context modular and continue
improving the boundary before introducing network and operational complexity.

## First vertical slice

Authentication and API identity are candidate early slices because they have a
meaningful security boundary and clear integration requirements:

```text
Submit credentials
→ verify identity
→ apply throttling policy
→ request MFA if required
→ verify MFA
→ issue token
→ record authentication event
```

The slice should include:

- Domain rules
- Application use case
- Legacy database adapter
- API endpoint
- Audit event
- Metrics and tracing
- Unit, integration, contract, and security tests
- Failure, timeout, revocation, and rollback behavior

The candidate must still be evaluated against other contexts and selected
using the project's baseline evidence.

## Data ownership transition

For each context, create a mapping from new concepts to legacy sources:

| New concept | Legacy source | Initial treatment |
|---|---|---|
| `AuthenticationAccount` | `users` | Read through a compatibility adapter |
| `UserId` | User identifier and UUID data | Define a stable identity mapping |
| `MfaEnrollment` | MFA-related tables | Validate before issuing tokens |
| `AccessToken` | OAuth token data | Assign one authoritative token owner |
| `AuthenticationEvent` | Audit and log data | Preserve required security history |

The transition should progress from documented compatibility to explicit
ownership:

1. Read legacy data through an adapter.
2. Define one authoritative writer for each concern.
3. Introduce new persistence only when ownership is clear.
4. Reconcile and validate data.
5. Migrate writes before reads when rollback is safe.
6. Retire legacy access paths after an observation period.

## Evaluation

The project will evaluate whether DDD improves the system rather than merely
counting domain classes.

Measures may include:

- Number of unrelated components affected by a change
- Number of dependencies crossing context boundaries
- Time to implement a new workflow
- Percentage of business rules covered by domain tests
- Number of direct legacy-table references in domain code
- Contract-test and integration discrepancies
- Data-migration defects
- Developer onboarding and review effort
- Ability to test or deploy a context independently

Results must include trade-offs, including added modeling effort, adapter
complexity, network overhead, operational burden, and areas where a module is
preferable to a service.

## Required artifacts

Each context investigation should produce or update:

- Context overview
- Bounded-context definition
- Ubiquitous-language glossary
- Workflow and event map
- Context-map relationship
- Data ownership and legacy mapping
- Security and privacy requirements
- API and event contracts
- Architecture Decision Records
- Vertical-slice test plan
- Baseline and outcome measurements
- Migration and rollback plan

## Decision record template

Use the following structure for context and deployment decisions:

```text
Decision:

Context:

Business capability:

Problem being addressed:

Alternatives considered:

Chosen model:

Data owned:

Legacy dependencies:

API and event contracts:

Security and privacy implications:

Operational implications:

Rollback strategy:

Evidence and metrics:

Review date:
```

## Guiding principle

> Use DDD to discover and enforce business boundaries first; use microservices
> only where those boundaries provide independent operational value.

OpenEMR Next is successful when it produces a safer, more maintainable, and
more extensible healthcare platform—not when it produces the maximum number of
services.
