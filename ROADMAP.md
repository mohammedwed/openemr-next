# OpenEMR Next Roadmap

This roadmap describes the proposed path for evaluating and incrementally
building OpenEMR Next. It is intentionally evidence-driven: a capability will
become an independently deployable service only when its boundary, operational
model, and measurable benefits are understood.

The roadmap is directional rather than a promise of fixed dates. Scope and
priority may change as domain discovery, stakeholder feedback, security review,
and migration experiments produce new evidence.

## Guiding principles

- Preserve clinical continuity and data integrity.
- Prefer incremental migration over a big-bang rewrite.
- Establish domain boundaries before extracting services.
- Treat the legacy database as a compatibility constraint, not the target
  architecture.
- Keep authentication, authorization, auditing, and privacy as first-class
  concerns.
- Measure the legacy baseline before claiming improvement.
- Prefer a modular component over a service when independent deployment or
  scaling does not provide a concrete benefit.
- Make every migration step observable, testable, and reversible.

## Phase 0: Project foundation

**Goal:** Establish the project, governance, and technical foundation.

### Work

- Confirm the project scope, stakeholders, and intended users.
- Document the relationship to OpenEMR and the independent-project status.
- Establish the Apache-2.0 licensing and third-party attribution process.
- Set up the C#/.NET solution structure.
- Define coding standards, branching rules, review requirements, and CI.
- Add automated formatting, static analysis, unit testing, and dependency
  scanning.
- Define security reporting and responsible-disclosure procedures.
- Establish development, test, and sanitized integration environments.

### Exit criteria

- The project builds reproducibly from a clean checkout.
- CI runs required quality checks for every change.
- Development and test-data handling rules are documented.
- Architectural decisions are recorded in version-controlled ADRs.

## Phase 1: Domain discovery and baseline

**Goal:** Understand the existing OpenEMR system before selecting service
boundaries.

### Work

- Map major business capabilities and clinical workflows.
- Identify candidate bounded contexts.
- Define the ubiquitous-language glossary.
- Create a context map showing dependencies and integration relationships.
- Inventory relevant legacy database tables and ownership assumptions.
- Map authentication, authorization, session, audit, and identity flows.
- Identify critical workflows and their correctness requirements.
- Collect baseline metrics for delivery, reliability, performance, security, and
  operations.
- Interview or gather feedback from representative clinical, administrative,
  support, development, and operations stakeholders.

### Exit criteria

- Candidate contexts and their responsibilities are documented.
- Terms with conflicting meanings are resolved or explicitly scoped by
  context.
- Critical workflows have owners and acceptance criteria.
- Baseline measurements have definitions, collection methods, and time
  windows.
- Initial migration risks and non-goals are recorded.

## Phase 2: Platform architecture

**Goal:** Define the target architecture and the rules that keep it maintainable.

### Work

- Define the modular application structure.
- Establish service and module dependency rules.
- Define API, event, error, versioning, and idempotency conventions.
- Define identity, authorization, audit, and correlation requirements.
- Select persistence and messaging patterns appropriate to each context.
- Define configuration, secrets, key management, and environment policies.
- Establish logging, metrics, tracing, health checks, and alerting standards.
- Define deployment, rollback, disaster-recovery, and support procedures.
- Document the coexistence model for legacy OpenEMR and new components.

### Exit criteria

- The target architecture is reviewed and recorded in ADRs.
- Contracts and cross-cutting standards have representative examples.
- Security and privacy requirements are mapped to technical controls.
- The team can explain which concerns belong in a module, service, or legacy
  adapter.

## Phase 3: First vertical slice

**Goal:** Prove the architecture with one bounded, end-to-end capability.

The first slice should be selected using evidence from Phase 1. Authentication
and API identity are candidate slices because they have a meaningful security
boundary and clear integration requirements, but the choice should not be
automatic.

### Work

- Define the slice's use cases and acceptance criteria.
- Implement the domain, application, infrastructure, and API boundaries.
- Build a controlled compatibility adapter to the legacy database where
  required.
- Add unit, integration, contract, security, and end-to-end tests.
- Implement audit events and operational telemetry.
- Document data reads, writes, ownership, and consistency behavior.
- Exercise failure, timeout, retry, revocation, and rollback scenarios.

### Exit criteria

- The slice runs in a production-like test environment.
- Critical behavior is covered by automated tests.
- Legacy and new behavior are compared for representative scenarios.
- Operational dashboards and alerts are available.
- Rollback is tested and documented.

## Phase 4: Controlled pilot

**Goal:** Validate the first slice under limited, observable usage.

### Work

- Select a low-risk pilot population or non-production integration path.
- Define explicit entry and rollback criteria.
- Use canary, shadow, or parallel validation where safe.
- Monitor correctness, security, latency, errors, resource usage, and audit
  completeness.
- Record incidents, discrepancies, support feedback, and operational effort.
- Compare pilot results with the legacy baseline.

### Exit criteria

- No unacceptable security, data-integrity, or clinical-continuity issues are
  identified.
- Results meet the pre-defined success thresholds.
- Known limitations and operational procedures are documented.
- A decision is made to expand, revise, or stop the slice.

## Phase 5: First production-capable capability

**Goal:** Make one capability independently deployable with appropriate
operational controls.

### Work

- Harden deployment, secrets, key rotation, backups, and recovery.
- Establish service ownership and on-call responsibilities.
- Complete threat modeling and security testing.
- Validate capacity and failure-isolation assumptions.
- Define API compatibility and deprecation policies.
- Formalize data ownership and limit uncontrolled legacy database writes.
- Publish operational runbooks and support documentation.

### Exit criteria

- The capability meets agreed production-readiness criteria.
- Security, privacy, and continuity reviews are complete.
- Deployment and rollback are repeatable.
- Ownership, monitoring, and incident response are assigned.
- The legacy application can continue operating safely during rollback.

## Phase 6: Expand by bounded context

**Goal:** Apply the validated approach to additional capabilities selectively.

### Candidate order

The order will be chosen using business value, coupling, risk, and measurable
benefit. Candidate contexts include:

1. Authentication and identity
2. Authorization and access policy
3. Notifications and background delivery
4. Scheduling
5. Patient administration
6. Interoperability and external integrations
7. Documents
8. Billing and claims
9. Clinical records
10. Reporting and analytics

This is not a fixed extraction order. Clinical records and billing may require
more extensive consistency and workflow analysis than an early technical
prototype.

### Work for each context

- Revisit the context boundary using lessons from previous phases.
- Define ownership and compatibility requirements.
- Select a vertical slice rather than migrating the whole context at once.
- Implement and test the slice.
- Measure against the baseline.
- Pilot and review operational impact.
- Decide whether to retain it as a module or deploy it as a service.

### Exit criteria

- The context has an explicit owner and contract.
- Migration evidence supports the selected deployment model.
- Data-consistency and rollback behavior are understood.
- The capability meets its agreed functional, security, and operational goals.

## Phase 7: Data ownership transition

**Goal:** Reduce dependence on the shared legacy database.

### Work

- Identify authoritative data for each migrated context.
- Define anti-corruption layers and synchronization rules.
- Introduce new persistence models where justified.
- Migrate data using repeatable, auditable procedures.
- Validate reconciliation and consistency.
- Move writes before reads only when ownership and rollback are safe.
- Retire legacy access paths after an observation period.

### Exit criteria

- Each migrated context has a clear source of truth.
- Shared writes are eliminated or formally controlled.
- Reconciliation and recovery procedures are tested.
- Legacy database dependencies are documented and shrinking.

## Phase 8: Platform evaluation and next strategy

**Goal:** Determine whether the architecture should be expanded, adjusted, or
constrained.

### Work

- Compare baseline, pilot, and post-migration metrics.
- Evaluate maintainability, extensibility, reliability, scalability, security,
  cost, and operational effort.
- Document failed hypotheses and unexpected trade-offs.
- Identify contexts that should remain modular.
- Review repository and team boundaries as services become independent.
- Update the long-term migration strategy based on evidence.

### Exit criteria

- Results are reproducible and documented.
- Benefits and costs are reported honestly.
- Remaining migration work has evidence-based priorities.
- The project has a reviewed plan for the next stage.

## Cross-cutting workstreams

These workstreams continue throughout every phase:

### Security and privacy

- Threat modeling
- Dependency and vulnerability management
- Secrets and key management
- Least-privilege access
- Authentication and authorization testing
- Auditability and incident response
- De-identified test data

### Clinical safety and continuity

- Critical-workflow validation
- Safe failure behavior
- Rollback and downtime procedures
- Data-integrity checks
- Stakeholder review of behavior changes

### Observability

- Structured logs
- Metrics and service-level indicators
- Distributed tracing
- Health checks
- Alerting and runbooks
- Correlation across legacy and new components

### Documentation

- ADRs
- Domain glossary
- Context map
- API and event contracts
- Data ownership records
- Migration runbooks
- Baseline and outcome reports

## Decision gates

Progression between phases requires an explicit review. A phase may be paused
or reversed when:

- Security or privacy controls are inadequate.
- Critical workflow behavior is not equivalent.
- Data consistency cannot be demonstrated.
- Reliability or performance regresses beyond tolerance.
- Operational burden exceeds the expected benefit.
- The boundary requires excessive synchronous coupling.
- Rollback is not practical.

The roadmap is successful when it produces a safer and more maintainable
platform, not when it produces the maximum number of microservices.
