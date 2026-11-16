# 14. Reference implementation map

- **Part of:** the Microservice Migration Playbook ([README](README.md))
- **Read this if:** you want to see a rule realized in real code, or reproduce this repository's build.
- **This is the only playbook document that names products, files, and decision records.** Everything else
  is framework-neutral.
- **Status as of:** 2026-11-16. The deviation register (section 5) was assembled from
  `docs/patient-management/07-gaps-and-evolution.md`, ADR text, and direct reads of the files named; items
  marked *checked* were confirmed by search on that date. Re-verify before relying on a status.

## 1. The stack, in neutral-to-concrete terms

| Neutral term used in the playbook | In this repository |
|---|---|
| Legacy system | OpenEMR (PHP), with its MariaDB database and "sites" file volume |
| The new service | **PatientManagement** (`src/Services/PatientManagement`) |
| Language / runtime | C# on .NET 10 |
| Web framework | ASP.NET Core minimal APIs |
| ORM | Entity Framework Core (MySQL provider), with raw parameterized SQL where legacy needs it |
| Gateway | YARP reverse proxy (`src/Services/Gateway`) |
| Identity component / BFF | **AuthFacade** (OpenIddict client, session cookie, OAuth2 code + PKCE against OpenEMR) |
| Frontend | Blazor WebAssembly app (`src/Services/WebApp`), MudBlazor |
| Disposable database container | Testcontainers (MariaDB) |
| API description + client generation | ASP.NET OpenAPI + NSwag, committed snapshots |
| Standards library | Firely .NET SDK (FHIR R4, US Core 8.0.0) |
| Circuit breakers | Polly-based (AuthFacade, WebApp) |
| Container orchestration (local/CI) | Docker Compose |
| CI | GitHub Actions (`.github/workflows/ci.yml`) |

## 2. Playbook rule groups → where they live

### 01 Principles and decisions

| Item | Realized in |
|---|---|
| P-1 slices, P-11 module vs service | `ROADMAP.md` "Guiding principles"; ADR 0060 (the service is deployed) |
| P-5 decision records | `docs/architecture/` (63 records, indexed in `README.md`); supersession pattern throughout |
| KD-1 unit | `docs/domain-driven-design.md` ("a bounded context may initially be a module…"); ADR 0060 |
| **KD-2 data location** | **ADR 0049** (writes into the legacy database); ADR 0050 (dedicated principal) |
| KD-3 trust and identity | ADR 0048 (bridge stays; gateway validation deferred), ADR 0055 (shared secret), ADR 0060 |
| KD-4 communication | ADR 0030 (BFF → gateway, one hop); `docs/patient-management/05-workflows-and-events.md` |
| KD-5 contract style | ADR 0052 (standards library and validation) |
| KD-6 test pattern | ADR 0006 (black-box stack), ADR 0050 (disposable containers for writers) |
| KD-7 parity | `docs/m4-full-legacy-parity-plan.md`; "(new)" in `docs/patient-management/04-domain-model.md` |
| KD-8 scope | ADR 0051 (portal excluded; merge deferred) |
| KD-9 deployment | ADR 0004 (one compose, one config), ADR 0020 (conservative defaults) |

### 02 Discovery and strategic design

| Item | Realized in |
|---|---|
| Schema inventory | `docs/m2-schema-inventory.md` (283 tables parsed from the snapshot) |
| Legacy behavior notes | `docs/m4-domain-notes.md`, `docs/m4-full-legacy-parity-plan.md` |
| The design set | `docs/patient-management/01…08` (the worked example of every artifact in 02 §4) |
| Strategic blueprint | `docs/openemr-ddd-design.md`; `docs/bounded-context-map.html` |
| Method | `docs/domain-driven-design.md` |
| Findings and decision registers | `docs/patient-management/07-gaps-and-evolution.md` §3, §6 |
| Not-verified list | the same document, §7 |

### 03 Hexagonal architecture

| Module (playbook) | Project |
|---|---|
| Domain | `OpenEmrNext.PatientManagement.Domain` |
| Domain.ReadModels | `…Domain.ReadModels` (ADR 0054) |
| Application.Ports | `…Application.Ports` (ADR 0010 introduced the separation) |
| Application | `…Application` (one `*UseCase` class per action) |
| Infrastructure | `…Infrastructure` (Ef\* repositories, outbox, file store) |
| Composition | `…Composition` (typed options, `ValidateOnStart`) |
| API | `…Api` (endpoints, contracts, authentication handler, OpenAPI transformers) |
| Client | `…Client` (NSwag-generated, ADR 0059) |
| Architecture tests | `tests/Services/PatientManagement/…ArchitectureTests` (`LayerDependencyTests`, `InvariantEnforcementTests`, `ClusterInvariantEnforcementTests`) |
| Layering rationale | ADR 0007 (superseded in part by 0035), ADR 0010, ADR 0054 |
| ARC-12 collapse | ADR 0035 (three of five bridge layers removed) |

> **Do not copy AuthFacade's structure as a template.** It is a collapsed migration-bridge scaffold with no
> domain. PatientManagement is the reference for a real domain service.

### 04 Domain modeling

| Rule | Realized in |
|---|---|
| DOM-2, DOM-3 validate on construction, collect errors | `PatientRegistration.Create`, `InsurancePolicy.Add`, `ResolvedCode.Create`; `*ValidationException` types |
| DOM-2 enforcement | `InvariantEnforcementTests` |
| DOM-5 narrow mutation | `InsurancePolicy.ChangeLevel` (`internal`), `AssignGeneratedIdentity` |
| DOM-9 reconstitution | `InsurancePolicy.Reconstitute` (internal) |
| DOM-12 pure rules with cited constants | `DuplicateScore`, `EffectiveInsuranceRule`, `PatientGenderMapping` |
| **DOM-13 cluster aggregate** | `InsuranceCoverage` (ADR 0053), `ClusterInvariantEnforcementTests` |
| DOM-14 no wire vocabulary | `ResolvedCode` field rename (`CodeSystem/CodeValue/DisplayText`); mapping in `FhirPatientMapper` |
| DOM-16 curated events | `PatientRegisteredEvent` |
| Review notes | `docs/m4-domain-notes.md` (self-assessments) |

### 05 Legacy integration and data

| Rule | Realized in |
|---|---|
| LEG-1 | `PatientManagementDbContext` (`ExcludeFromMigrations` on `patient_data`) |
| LEG-3 | `Rfc4122GuidConverter` + `Rfc4122GuidConverterTests`; backing-field mapping for `email` columns |
| LEG-4 | `EfInsurancePolicyRepository.GetPoliciesAsync/GetRowsAsync` (dates read as text); tolerance tests |
| LEG-5 | Raw SQL in `Ef*Repository` (parameterized; audited in `m4-domain-notes.md`) |
| **LEG-6, LEG-18** | `scripts/seeds/patient-management-db.sh` (per-table grants with reasons; idempotent one-shot); `docker-compose.yml` service `seed-patient-management-db` |
| LEG-12 | `sequences` counter idiom in `EfAdvanceDirectiveRepository` (the only user; no test pins the idiom yet, which is itself a small gap) |
| LEG-14 | `LocalDiskAdvanceDirectiveFileStore` (containment, delete), `EfAdvanceDirectiveRepository` (compensation); compose `user: "1000:1000"` and `sitesvolume` mount |
| LEG-13 | `OutboxMessage`; `patient_management_outbox` created by the seed script |
| LEG-17 | `EfPatientFhirRepository.LoadListOptionsAsync` (N+1 removal) |
| Ownership matrix | `docs/patient-management/06-data-ownership-and-legacy-mapping.md` §2 |

### 06 APIs, gateway, events

| Rule | Realized in |
|---|---|
| API-1/2/3 | `/api/v1` group (`PatientManagementEndpoints`); `Contracts/Requests|Responses` |
| API-4, OPS-8 | `AddProblemDetails()` + `UseExceptionHandler()` (ADR 0057), `GlobalExceptionHandlingTests` |
| ARC-11 | `AddValidation()` and DataAnnotations on request DTOs (`m4-domain-notes.md` "Api follow-up") |
| API-9 | `PatientManagementOpenApi`, `ValidationAttributesSchemaTransformer` (ADR 0058), `OpenApiDocumentTests` |
| API-10 | `OpenEmrNext.PatientManagement.Client`; `scripts/dev/generate-patientmanagement-openapi.sh` (ADR 0059) |
| API-11 | `FhirPatientMapper`, `FhirProfileValidator`, vendored `FhirProfiles/*.tgz` (ADR 0052) |
| API-14/15/16/17 | `src/Services/Gateway/…/Program.cs` (specific routes, per-cluster secret transform, original Host, TLS, HSTS); ADR 0033, 0034, 0039, 0060 |
| API-18 | ADR 0030 |
| EVT-1 | `EfPatientRegistry` (patient + outbox in one `SaveChanges`) |
| EVT-2…EVT-14 | **Target** in `docs/patient-management/05-workflows-and-events.md` §5-6 (not yet built) |

### 07 Security

| Rule | Realized in |
|---|---|
| SEC-3 | `InternalGatewayAuthenticationHandler` (constant-time compare; no credential vs wrong credential) |
| SEC-1 | `.RequireAuthorization()` on both route groups (`PatientManagementEndpoints`, `FhirPatientEndpoints`) |
| SEC-7 | Path containment in `LocalDiskAdvanceDirectiveFileStore`; content-hash-keyed profile cache in `FhirProfileValidator` |
| SEC-11/13 | ADR 0038, 0039 (AuthFacade rate limits, forwarded headers) |
| SEC-14 | ADR 0015 (superseded with the password grant), ADR 0020 |
| SEC-15 | Dockerfile non-root; compose has no published port for `patient-management` |
| Secrets | `scripts/setup/new-env.sh`; `.env.example`; `SECURITY.md` |
| Auth migration (separate topic) | `docs/legacy-auth-migration-handbook.md` and companions |

### 08 Quality and testing

| Rule | Realized in |
|---|---|
| QLT-1 | `tests/…/PatientManagement.UnitTests/Domain/**` |
| QLT-2, QLT-5 | `…ArchitectureTests` |
| QLT-7 | `PatientDataContainerFixture` (MariaDB container, legacy-shaped `patient_data` without a primary key) |
| QLT-9 | `…IntegrationTests/*EndpointTests`, `OpenApiDocumentTests`, `GatewaySecretAuthenticationTests`, `GlobalExceptionHandlingTests` |
| QLT-10 | `tests/Services/Gateway/…IntegrationTests/GatewayStackTests`; `…AuthFacade.IntegrationTests` |
| QLT-12 | `GlobalExceptionHandlingTests` (always-throwing port); retry verified in ADR 0056 |
| Six-dimension review | `docs/m4-domain-notes.md` ("Architecture self-assessment", "Application/Ports", "Infrastructure", "Resilience", "Api") |

### 09 Delivery and operations

| Rule | Realized in |
|---|---|
| OPS-1 | `src/Services/PatientManagement/Dockerfile` and its own `.dockerignore` (ADR 0060 documents why) |
| OPS-2, OPS-6 | `docker-compose.yml` (`patient-management`, `seed-patient-management-db`, `gateway`) |
| OPS-3 | `Composition/*Options.cs` + `ValidateOnStart()`; ADR 0037 (depend on the seed completing) |
| OPS-4, OPS-13 | `.env.example`, `scripts/setup/*`, `docs/configuration.md`, ADR 0004, 0020 |
| OPS-7 | ADR 0036 (wait for the legacy readiness probe) |
| OPS-9 | ADR 0056 (`CreateExecutionStrategy().ExecuteAsync`, identity assigned after success) |
| OPS-10 | ADR 0017/0041 (superseded and current breakers); `TransientFailureCircuitBreakingHandler` in WebApp |
| OPS-12 | `.github/workflows/ci.yml` |
| OPS-17 | `scripts/README.md` |
| OPS-19 | ADR 0028, 0043-0046 (single-instance assumptions closed for AuthFacade) |
| OPS-20 | `global.json`; release snapshot `snapshot-openemr-8.0.0.3-ci` |

### 10 Roadmap

| Item | Realized in |
|---|---|
| Milestones | `ROADMAP.md` (M0, M0a-c, M1, M2, M3, M4, M4 frontend, M5, M6) and decision gates |
| Per-day record | `docs/changelogs/` |
| Schedule | `docs/schedule-m2-m3-dec-2026.md` |

## 3. A reproduction path

To reproduce this repository's build in a new project, follow the **playbook's** order (10 §3). To see the
real, chronological sequence this repository followed, read in this order. Each step lists what to copy,
and what to do differently.

| Step | Playbook | What happened here | Read | Do differently |
|---|---|---|---|---|
| 1 | M0 | Compose stack, restorable DB baseline, black-box tests with a readiness gate | ADR 0004, 0006, 0020; `docs/fast-legacy-stack-startup.md`; M0-M0c in `ROADMAP.md` | Nothing; this worked |
| 2 | M1 | A token broker first, then replaced by a browser flow, BFF, hardening; stand-in deleted | ADR 0001-0048; the three `legacy-auth-*` documents | Name the stand-in's removal criterion at the start (MIG-11); do not layer a thin bridge (ARC-12) |
| 3 | M2 | Schema inventory of 283 tables; boundary decision gate | `docs/m2-schema-inventory.md` | Also inventory **every writer** (LEG-8) |
| 4 | M3 | Gateway fronting UI and identity; one external origin | ADR 0033, 0034, 0039, 0030 | Decide the signed-in check **before** routing a domain service (SEC-2) |
| 5 | M4 slice 1 | Registration: aggregate, outbox, least-privilege principal, container | ADR 0049, 0050; `docs/m4-domain-notes.md` | Inject an identity allocator (DOM-7); no inline post-commit work (EVT-15); test as the real principal (QLT-8) |
| 6 | M4 slices 2-5 | Duplicate scoring, insurance, advance directives, FHIR API; scope decisions | ADR 0051, 0052, 0053; `docs/m4-full-legacy-parity-plan.md` | Route **all** write paths through the aggregate (DOM-18) |
| 7 | M4 reviews | Layer-by-layer self-assessments; read models split; retry; error handler; OpenAPI; client | ADR 0054, 0056-0059 | Do the reviews at each slice (MIG-12), not in batches |
| 8 | M3/M4 deploy | Dockerfile, compose, gateway route | ADR 0055, 0060 | Close the gate (SEC-2) in the same change as the route |
| 9 | M4 frontend | List, patient view, registration, insurance/directives pages | ADR 0061-0063; `docs/changelogs/2026-11-1*` | Frontend after gate C (MIG-13) |
| 10 | Design set | The DDD design set, written after the build | `docs/patient-management/` | Write a thin version **first** (DSC-1) |

## 4. Reproducing the PatientManagement skeleton in a new context

1. Create the seven modules and wire references exactly as in 03 §2.
2. Copy the architecture tests (`LayerDependencyTests`, `InvariantEnforcementTests`) and change the
   namespaces.
3. Create the composition options, one class per concern, each `[Required]` + validated on start.
4. Create the provisioning script by copying `scripts/seeds/patient-management-db.sh` and replacing
   tables and grants; keep one comment per grant explaining the privilege.
5. Create the Dockerfile and **its own `.dockerignore`**; add the compose service **without a published
   port**.
6. Add the gateway route, the per-cluster secret transform, and (this time) the signed-in check.
7. Create the test fixture with a legacy-shaped schema and the **production principal**.
8. Implement the first slice following 10 §4, using `RegisterPatientUseCase`, `IPatientRegistry`,
   `EfPatientRegistry`, and `PatientEndpoints` as the shape.
9. Generate the API snapshot and client; add the CI drift check.
10. Write the decision records and update the design set in the same change.

## 5. Deviation register: where this repository does not yet follow the playbook

Honest status. Each item has an owner in `docs/patient-management/07-gaps-and-evolution.md` unless marked
*new*. **Do not copy these gaps.**

| Rule | Deviation | Tracked as |
|---|---|---|
| **SEC-2** | The gateway attaches the secret unconditionally; the patient API is reachable without signing in | M1 / P0-1 (**critical**, decision DEC-10) |
| SEC-4, API-8 | `registeredBy`, `ownerId`, `authenticatorUserId` are client-supplied; the UI sends a placeholder id | M2 / ADR 0055 |
| SEC-5 | No authorization policy | M-az |
| SEC-9, LEG-16 | No audit trail; legacy `log` bypassed; `updated_by` never written | G-8 |
| EVT-2, EVT-7 | No envelope, no relay; the outbox is written and never read | F-6, G-5 |
| EVT-9 | Only registration emits | F-5 |
| EVT-15 | Duplicate scoring runs inline after commit | F-1 |
| API-5 | No idempotency key | F-2 |
| API-3 | State conflicts return 400, not 409 | F-7 |
| API-6 | The patient list endpoint returns everything | F-9 |
| LEG-9, DOM-7 | Random 63-bit patient numbers generated in the aggregate | G-2, 14.2 |
| LEG-10 | No concurrency token; no update command yet | PT-11 |
| LEG-11 | UTC and server-local clocks unasserted | G-7 |
| LEG-15 | Legacy-UI compatibility of service-created rows never checked | G-10 (P0-3) |
| LEG-4, DOM-9 | Loading `Patient` through the ORM throws on dirty legacy rows (insurance is tolerant) | G-4 |
| LEG-6 | The outbox grant is `ALL PRIVILEGES`; should be `SELECT, INSERT, UPDATE, DELETE` | G-6 |
| LEG-7, QLT-8 | Integration fixture does not use the real least-privilege user | G-1 |
| OPS-5 | The outbox table uses `CREATE TABLE IF NOT EXISTS`; it cannot evolve | G-5 |
| DOM-4 | Identifiers are plain integers | 14.1 |
| QLT-3 | Tests do not cite rule ids | 04-trace |
| QLT-22 | Duplicate-scoring test flake (phonetic collision) tracked, not fixed | `m4-domain-notes.md` |
| OPS-8 | AuthFacade and Gateway have no global exception handler | ADR 0057 consequences |
| OPS-7 | PatientManagement has no health or readiness endpoint | ADR 0059 (the generator polls the OpenAPI document instead) |
| OPS-12, API-9 | CI regenerates and diffs only the AuthFacade snapshot, **not** the PatientManagement one (*checked*) | *new* |
| OPS-17 | `scripts/README.md` does not list `seeds/patient-management-db.sh` or `dev/generate-patientmanagement-openapi.sh` (*checked*) | *new* |
| OPS-18 | `docs/configuration.md` and `docs/environments.md` have no PatientManagement or `INTERNAL_GATEWAY_SECRET` entries; `.env.example` does (*checked*) | *new* |
| LEG-14 | No reconciliation report for orphan files or rows; compensating delete has no automated test | F-4, G-9 |
| DSC-5 | Rule ids exist in the design set but not in code or test names | 04-trace |

**The three items marked *new* were found while writing this playbook**; they are not in the findings
register yet. Adding them is the first practical use of this guide: file them in
`docs/patient-management/07-gaps-and-evolution.md` §3.

## 6. Where the repository followed the playbook well

Worth copying as-is: layered modules with enforced dependencies; the invariant-enforcement test; the
multi-entity aggregate with persistence workaround kept in the adapter; tolerant insurance reads; the
per-grant commented provisioning script; startup-validated options; the retry-safe transaction pattern;
the generated-API-description tests; the vendored, hash-keyed standards profiles; decision records with
alternatives and consequences; the self-assessment reviews that found real defects; the design set's
status vocabulary (Built, Target, Deferred) and not-verified list.

## 7. Existing documents that complement this playbook

| Document | Use it for |
|---|---|
| `docs/legacy-auth-migration-handbook.md` | Authentication migration, end to end (read before KD-3) |
| `docs/legacy-auth-facade-framework.md` | The generic, citation-free version of the same |
| `docs/auth-facade-implementation-guide.md` | Sequential build order for the identity bridge |
| `docs/fast-legacy-stack-startup.md` | Getting the legacy stack running quickly |
| `docs/development-workflow.md` | Branching, review, and CI contract this playbook assumes |
| `docs/patient-management/` | The worked example of the whole design set |
