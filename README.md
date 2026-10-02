# OpenEMR Next

An independent modernization project exploring a scalable, maintainable, and
extensible C# platform inspired by OpenEMR's domain, workflows, and
interoperability requirements.

> **Status:** Architecture and discovery phase  
> **This project is not yet intended for production clinical use.**

OpenEMR Next is not an official OpenEMR rewrite, fork, replacement, or
affiliated project unless explicitly stated.

## Background

OpenEMR is a mature electronic medical record platform with extensive clinical,
administrative, billing, interoperability, and patient-facing functionality.
Its established codebase and shared legacy architecture provide significant
business value, but they also make certain changes difficult to isolate, test,
scale, and deploy independently.

This project investigates whether a modular, selectively service-oriented C#
architecture can address those challenges without requiring a risky big-bang
rewrite.

The project is guided by real operational goals rather than by microservices as
an end in themselves.

## Objectives

The project will evaluate and develop an architecture that supports:

- Clear domain and bounded-context boundaries
- Maintainable and testable C# services and modules
- Independent evolution of healthcare capabilities
- Versioned APIs and integration contracts
- Improved interoperability with external healthcare systems
- Consistent authentication, authorization, and auditing
- Independent scaling where it provides a measurable benefit
- Better observability and operational diagnostics
- Safe, incremental migration from the existing OpenEMR platform
- Continued compatibility with the legacy OpenEMR database during transition

## Research question

> To what extent can an incremental migration of a legacy healthcare platform
> to a modular, service-oriented C# architecture improve maintainability,
> extensibility, scalability, and delivery capability while preserving
> correctness, security, reliability, and clinical continuity?

## Supporting questions

- Which bounded contexts can be identified in the existing OpenEMR domain?
- Which capabilities should remain modules rather than become independent
  services?
- Can new C# components coexist safely with the legacy application and
  database?
- Does the new architecture reduce the scope and risk of changes?
- Can selected workloads scale independently?
- Does the architecture improve integration delivery and testability?
- What operational and organizational costs does the new architecture
  introduce?
- Which migration patterns are repeatable for additional bounded contexts?

## Architectural direction

The target is a **modular architecture with selectively deployable services**.

Not every bounded context will automatically become a microservice. A context
should be deployed independently only when doing so provides a concrete
benefit, such as:

- Independent scaling
- Independent deployment
- Separate operational ownership
- Strong security isolation
- A stable external integration boundary
- Different reliability or performance requirements

A context may initially be implemented as:

- A module within the new application
- An independently tested package
- A compatibility adapter
- A separately deployable service

## Initial bounded contexts

The following are candidate contexts for investigation:

- Identity and authentication
- Authorization and access control
- Patient administration
- Clinical records
- Scheduling
- Billing and claims
- Documents
- Notifications
- Reporting and analytics
- Interoperability and external integrations

These are initial hypotheses, not final service boundaries. Each context will
be validated through domain analysis, workflow mapping, stakeholder input, and
dependency analysis.

## Initial authentication boundary

Authentication is a candidate early migration area because it provides a clear
security and integration boundary.

The initial authentication capability may include:

- Credential verification
- MFA verification
- Account lockout and throttling
- OAuth2/OIDC token issuance
- Token validation and revocation
- Identity claims
- Authentication audit events

The first version should not automatically own:

- Clinical authorization decisions
- Facility-specific policy
- Patient-context access rules
- All legacy browser session behavior
- Clinical user-profile management
- Every existing OpenEMR authentication mode

During the transition, the authentication component may use controlled
adapters to existing OpenEMR identity and authentication data. Database access
and write ownership will be explicitly documented.

## Migration strategy

The migration will follow an incremental strangler approach:

```text
Existing OpenEMR
        │
        │ legacy database and workflows
        ▼
Compatibility boundaries
        │
        ▼
New modular C# components
        │
        ▼
Selected independently deployable services
```

The migration will prioritize:

1. Understanding the existing domain and workflows
2. Defining bounded contexts and ubiquitous language
3. Establishing API and event contracts
4. Building the C# platform foundation
5. Implementing representative vertical slices
6. Validating coexistence with the legacy application and database
7. Measuring outcomes against the legacy baseline
8. Expanding migration only when the evidence supports it

A full rewrite is intentionally avoided because the existing system contains
valuable clinical functionality and data that must remain available and
correct.

## Legacy database compatibility

The legacy OpenEMR database may be used during the transition, but it is not
intended to become a permanent shared integration mechanism.

For each bounded context, the project will document:

- Tables read
- Tables written
- Data ownership
- Allowed database operations
- Compatibility assumptions
- Consistency requirements
- Migration and rollback procedures
- Planned path toward independent data ownership

Shared database access will be treated as a migration constraint, not as the
target architecture.

## Technology direction

The target implementation will use C# and the .NET ecosystem, with technology
choices evaluated against project needs.

Potential benefits include:

- Strong typing for complex clinical and financial domains
- Mature API and web development tooling
- Dependency injection and testing support
- Health checks and observability integrations
- Background-processing capabilities
- Standards-based interoperability support
- Flexible deployment options

C# is not considered a justification by itself. The technology choice will be
evaluated together with migration cost, maintainability, team capability,
operational complexity, and compatibility requirements.

## Evaluation approach

The project will establish a baseline using the existing OpenEMR
implementation and compare equivalent workloads during migration.

### Maintainability

- Change-impact scope
- Dependency and coupling measurements
- Number of bounded contexts affected by a change
- Developer onboarding effort
- Code review and implementation time

### Delivery

- Lead time for changes
- Deployment frequency
- Deployment duration
- Change failure rate
- Rollback frequency

### Reliability

- Availability of critical workflows
- Error rates
- p95 and p99 latency
- Mean time to recovery
- Regression defects

### Scalability

- Throughput under representative load
- Concurrent-user capacity
- CPU and memory usage
- Database load
- Independent scaling effectiveness

### Security

- Authentication and authorization correctness
- MFA behavior
- Token revocation time
- Audit-event completeness
- Key-rotation procedures
- Security findings and remediation time

### Migration safety

- Functional equivalence
- Data-consistency results
- Rollback duration
- Legacy compatibility
- Dual-run or contract-test discrepancies

The project will report both improvements and trade-offs. A successful result
may show that some capabilities benefit from independent services while others
should remain modular components.

## Success criteria

The architecture will be considered successful only if it demonstrates that:

- Critical workflows remain functionally correct
- No unauthorized access is introduced
- Required audit events are preserved
- New components can coexist with OpenEMR during migration
- Service and module boundaries are explicit and enforceable
- Representative changes affect fewer unrelated components
- Selected workloads meet defined performance and availability targets
- Deployment and rollback procedures are repeatable
- The migration approach can be applied to another bounded context
- Operational complexity remains manageable

## Non-goals

This project does not initially aim to:

- Rewrite all of OpenEMR at once
- Convert every bounded context into a microservice
- Replace the legacy database immediately
- Preserve undocumented behavior without analysis
- Introduce distributed transactions as a default pattern
- Optimize for technology adoption alone
- Make production clinical claims before appropriate validation
- Store or use identifiable patient data in development or evaluation
  environments

## Repository structure

The planned repository may use a monorepo structure:

```text
/
├── services/
│   ├── authentication/
│   └── ...
├── modules/
├── contracts/
├── compatibility/
├── database/
├── infrastructure/
├── tests/
├── docs/
│   ├── architecture/
│   ├── domain/
│   ├── migration/
│   └── case-study/
└── README.md
```

Services remain independently deployable even when maintained in the same
repository.

## Documentation

Architecture and research documentation will include:

- Bounded-context definitions
- Ubiquitous-language glossary
- Context map
- Architecture Decision Records
- API and event contracts
- Migration plans
- Database ownership rules
- Evaluation methodology
- Baseline and post-migration metrics
- Security and continuity controls
- Lessons learned

## Security and clinical continuity

This project concerns healthcare software and must prioritize:

- Confidentiality and least privilege
- Authentication and authorization correctness
- Auditability
- Data integrity
- Safe failure behavior
- Controlled access to test data
- De-identification of datasets
- Reversible migration
- Operational monitoring
- Clinical workflow continuity

No component should be considered production-ready based solely on successful
compilation or functional demonstrations.

## Current phase

The current phase focuses on:

- Architecture discovery
- Domain and bounded-context analysis
- Ubiquitous-language definition
- Baseline metric identification
- Migration-risk analysis
- Authentication and authorization boundary design
- Selection of an initial vertical slice

Implementation will begin after the target boundary, evaluation criteria, and
migration safeguards have been reviewed.

See the [project roadmap](ROADMAP.md) for the proposed phases, decision gates,
and migration workstreams.

The project's DDD approach is documented in
[Domain-Driven Design Guide](docs/domain-driven-design.md).

## License

OpenEMR Next is licensed under the [Apache License 2.0](LICENSE).

This project is an independent implementation developed from scratch. It
supports compatibility with selected OpenEMR database structures and workflows
but does not copy OpenEMR source code.

OpenEMR Next is not affiliated with, sponsored by, or endorsed by the OpenEMR
project unless explicitly stated.
