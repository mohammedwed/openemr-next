# OpenEMR Next Roadmap

This roadmap has two layers:

1. **Current scope:** the milestones for the portfolio vertical slice. Status
   comes from the dated changelogs, which are the source of truth.
2. **Long-term phases:** the broader migration vision. These are directional and
   will be revisited once the slice produces evidence.

The roadmap is evidence-driven. A capability becomes an independently deployable
service only when its boundary, operational model, and benefits are understood.
Dates are not promises.

## Guiding principles

- Preserve clinical continuity and data integrity.
- Prefer incremental migration over a big-bang rewrite.
- Treat the legacy database as a compatibility constraint, not the target.
- Keep authentication, authorization, auditing, and privacy first-class.
- Prefer a module over a service unless independent deployment or scaling pays
  off.
- Use only synthetic or sanitized data in development and evaluation.
- Make every step observable, testable, and reversible.

## Current scope: portfolio vertical slice

**Goal:** One runnable, tested, and documented slice showing patient registration
and encounter creation, reached through an authenticated gateway, with OpenEMR as
a compatibility constraint.

**Deployable pieces:** AuthFacade, API gateway, Patient Management, Clinical Care.

**Estimate:** roughly 4–6 months part-time. Re-estimate after M2 and M3.

### Status

| # | Milestone | Status | Evidence |
|---|---|---|---|
| M0 | Local stack in one Compose project | Done | Oct 4, Oct 10, Oct 11 |
| M0a | Database baseline, reproducible and tested | Done (cleanup pending) | Oct 4 |
| M0b | AuthFacade login through OpenEMR's OAuth server, facade-issued JWT | Done | Oct 10, Oct 11 |
| M0c | Black-box integration tests with readiness gate | Done | Oct 11 |
| M1 | Harden the identity bridge | In progress | Oct 10, Oct 11, Oct 21 |
| M2 | Schema inventory for slice contexts | In progress | Oct 4 (entities scaffolded) |
| M3 | Gateway and token validation | Not started | — |
| M4 | Patient Management service | Not started | — |
| M5 | Clinical Care service | Not started | — |
| M6 | Portfolio polish | Not started | — |

**Done beyond the original plan:** a privilege boundary that limits the
AuthFacade client to authentication scopes, build isolation for the AuthFacade
container, and a readiness gate that stops integration tests from running
against a half-started stack.

### M0a: Database baseline (done, cleanup pending)

- Schema and seed data from the real OpenEMR installer, dumped from MariaDB 12.3
- Loaded automatically on first start; schema-only dump kept as a diff target
- Import test (`Db.Tests`) checks table count and that key seed tables are not
  empty

**Still to do before sharing:** remove the `Administrator` user and hash,
log and session rows, and environment-specific `globals`; confirm there are no
patient rows; have the admin created from environment variables on first run.

### M0b: AuthFacade login (done)

- `POST /connect/token` verifies credentials through OpenEMR's password grant
  and returns a facade JWT in the response body (superseded: see ADR 0013, which returns OpenEMR's token response)
- `GET /me` validates the facade JWT and returns the subject and name (superseded: `/me` now validates OpenEMR's token)
- Identity stays in OpenEMR; clients receive OpenEMR's token, verified before it is returned (ADR 0013)

**Decision recorded:** the response is JSON, not a browser redirect. The
redirect and cookie handling considered earlier are not part of this milestone.

### M0c: Integration tests (done)

- `OpenEmrNext.AuthFacade.IntegrationTests` runs against the live stack and covers the seed,
  OpenEMR's OAuth server, the password grant, and `/me` signature validation
- The readiness gate checks four conditions under one deadline before tests run

### M1: Harden the identity bridge (in progress)

**Done**

- Credentials are never stored or logged by AuthFacade
- Client scopes are minimal
- Integration tests verify signatures and the subject claim
- Layer dependency rules enforced by architecture tests (ADR 0007)
- Login credential rules follow OpenEMR's limits, not a new-account policy (ADR 0008)
- Malformed OpenEMR success responses return 502 instead of 500
- Password plaintext is retrieved once and cleared (ADR 0009)
- Unit tests (71), architecture tests (6) and live integration tests (13) pass

**Remaining**

- Replace the development TLS bypass with a trusted certificate path for any
  shared environment
- Fixed audience string replaced by a registered-client check
- Persist data-protection keys
- Confirm that request logging does not capture form bodies, with a test
- Start-up ordering: AuthFacade waits for the database but not for OpenEMR; add a
  check or retry so early requests fail clearly
- Decide the refresh and revocation approach, or record that it is deferred
- ADR: identity bridge design
- Login rate limiting per client address: done, see ADR 0018
- Login rate limiting per username and lockout at the facade. Open: today only
  the per-address limit and OpenEMR's own lockout apply.
- Audit log of login outcomes and `/me` access, with no credentials or tokens
  in the log
- HTTPS for the service itself, with a redirect and HSTS. Required before any
  shared environment, together with the TLS item above
- Record the accepted risk of password copies in memory (ADR 0009) and the
  complexity-policy gap (ADR 0008) in the M1 exit review

**Exit criteria:** The security items above are either done or recorded as
accepted risks. The ADR is merged.

### M2: Schema inventory (in progress)

**Done:** EF entities scaffolded from the live database.

**Remaining**

- Map every table in the slice's scope to a bounded context
- List tables written by more than one context
- List cross-context foreign keys, each with a decision: reference ID, duplicated
  data, or API call
- Record legacy anti-patterns as ADR input

**Time-box:** 3–4 weeks beyond the scaffold. A good-enough inventory is acceptable.

**Decision gate:** Confirm the Patient–Encounter boundary. If the schema forces a
different split, update the design before writing service code.

### M3: Gateway and token validation (not started)

- YARP gateway routing to AuthFacade and to the new services
- Token validation at the gateway (AuthFacade currently validates its own tokens
  on `/me` only)
- Compose entry for the gateway
- ADR: whether AuthFacade stays as the permanent legacy bridge or is replaced by
  a dedicated identity service

**Decision gate:** Record the identity architecture in an ADR.

### M4: Patient Management service (not started)

- Own database and EF Core migrations, with a slim model scaffolded from
  `patient_data` only
- `Patient` aggregate and registration use case
- Read-only anti-corruption adapter for `patient_data`
- Transactional outbox publishing `PatientRegistered`
- Unit and integration tests (Testcontainers)

**Exit criteria:** A patient is registered through the gateway. The event is
published exactly once. Tests pass.

### M5: Clinical Care service (not started)

- Own database and migrations, slim model from `form_encounter` and related
  tables
- `Encounter` and `Vitals` aggregates
- Anti-corruption adapter for `form_encounter`
- Consumes `PatientRegistered` through the outbox
- Shadow comparison: legacy and new encounter creation match for the synthetic
  scenarios

**Exit criteria:** An encounter is created for a registered patient and vitals
are recorded, end to end. The shadow comparison shows no unexplained
discrepancies.

### M6: Portfolio polish (not started)

- One-command startup for the full slice
- Architecture tests (ArchUnitNET) enforcing dependency rules
- Cross-service integration tests
- ADR index and updated README
- Demo walkthrough (script or recording)
- Case study: what worked, what did not, and the trade-offs

**Exit criteria:** A new reader can clone the repository, run the slice, and
follow the demo in under 15 minutes.

## Architecture Decision Records

ADRs are written alongside each milestone. The changelogs already contain the
reasoning for these decisions, so they should be converted rather than rewritten
from memory.

From 4 October:

- Baseline dump produced by the real installer, not hand-written SQL
- Entity model shared for now; per-service slim models before splitting

From 10 October:

- OpenIddict dropped in favour of delegating to OpenEMR's OAuth server
- Direct reads of OpenEMR's `users` table rejected
- Facade-issued JWTs instead of passing OpenEMR's token to clients
- Facade as adapter over OpenEMR, with OpenEMR left unmodified
- OpenEMR run as its own stack, not copied into the project
- Pinned release image over the development image
- Client secrets generated, never typed into chat

From 21 October:

- Layered AuthFacade structure, with dependency rules enforced by tests
- Login credential rules follow OpenEMR's limits; complexity policy deferred
- Password plaintext retrieved once; copies in memory accepted as a limit

From 11 October:

- One root Compose file for the whole solution
- One configuration source, gitignored secrets
- AuthFacade as a token broker, not an identity provider
- OAuth client provisioned by an idempotent seed
- Minimal scopes for the AuthFacade client
- Black-box integration tests with a readiness gate
- Build isolation for the AuthFacade container

## Open questions

- **Browser login:** If users sign in through a web page, AuthFacade needs the
  authorization-code flow with PKCE, including redirect and consent design. Not
  yet scheduled.
- **Ownership of the OAuth client:** OpenEMR owns the row today. If AuthFacade
  becomes a standalone identity service, ownership and the seed move with it.
- **Client-registration correctness:** The seed inserts a `oauth_clients` row
  directly. The 10 October decision noted that secret hashing varies between
  OpenEMR versions. The seed works with the pinned version, but this is an
  accepted risk, not a resolved one. Revisit it when the OpenEMR version changes.
- **Seed contents in images:** The seed holds environment configuration. Decide
  whether it belongs in build artifacts.
- **When to split the Compose file:** The trigger is a service that needs its own
  deployment target, not a service count.

## Risks

- **Upstream OpenEMR defect:** Some wrong-password requests return 500. The
  integration test will fail if this recurs. Track it upstream.
- **Brute force against login:** The facade limits each client address to 10
  login requests a minute (ADR 0018). A distributed attack across many addresses
  against one account is not limited until the per-username item is done, so
  OpenEMR's lockout remains the only control for that case.
- **M2 can expand:** Time-box it and accept a good-enough inventory.
- **Synthetic data quality:** M4 and M5 tests pass without proving much unless
  the data includes messy edge cases.
- **Start-up ordering:** Requests can fail during OpenEMR's start window until
  M1 adds a proper gate.
- **Solo capacity:** Each milestone should end in a working, demonstrable state.
  If time runs short, reduce M5 and M6 rather than leaving M0–M4 half-finished.
- **Scope creep:** Work outside the four deployable pieces needs an ADR first.
- **Password grant:** Deprecated in OAuth 2.1. Acceptable here, but it must not
  become a pattern for browser or third-party clients.

## Long-term phases

These are not scheduled for the current scope. Each assumes evidence from earlier
work and will be revisited at decision gates.

- **Phase 0, Foundation:** licensing, CI with formatting, static analysis, tests,
  and dependency scanning. Partly in place.
- **Phase 1, Discovery and baseline:** capability and workflow mapping, context
  map, glossary, legacy inventory, and baseline metrics. The slice's part of this
  phase is M2. Stakeholder interviews are needed before any production claim.
- **Phase 2, Platform architecture:** module rules, API and event conventions,
  identity and audit requirements, and the coexistence model. The slice exercises
  these in a limited form.
- **Phase 3, First vertical slice:** the current scope (M0–M6).
- **Phase 4, Controlled pilot:** canary, shadow, or parallel running with real
  users in a sanitized environment. Out of scope for the portfolio phase. The M5
  shadow comparison is the closest equivalent.
- **Phase 5, Production-capable capability:** hardened deployment, secrets and key
  rotation, backups, threat modeling, and on-call. Not planned for this scope.
- **Phase 6, Expand by context:** apply the approach to other contexts, selected
  by value, coupling, risk, and benefit.
- **Phase 7, Data ownership transition:** establish authoritative sources, sync
  rules, reconciliation, and retirement of legacy access paths.
- **Phase 8, Evaluation:** compare baseline, pilot, and post-migration metrics;
  record failed hypotheses; set the next strategy from evidence.

## Cross-cutting workstreams

- **Security and privacy:** threat modeling, dependency scanning, secrets,
  least-privilege access, authentication tests, de-identified data.
- **Clinical safety:** critical-workflow validation, safe failure, rollback,
  data-integrity checks.
- **Observability:** structured logs, health checks, correlation across legacy and
  new components. Metrics and tracing come later.
- **Documentation:** ADRs, glossary, context map, contracts, data-ownership
  records, changelogs, case study.

## Decision gates

Work can be paused or reversed when security controls are inadequate, critical
workflows are not equivalent, data consistency cannot be shown, reliability
regresses beyond tolerance, operational burden outweighs the benefit, coupling
becomes excessive, or rollback is impractical.

Named gates in the current scope:

- After **M2:** confirm the Patient–Encounter boundary.
- After **M3:** record the identity architecture decision.
- After **M5:** decide whether the slice meets its success criteria, then proceed
  to polish or revise the design.

The roadmap succeeds when it produces a safer, more maintainable platform and an
honest account of what the approach does and does not deliver.
