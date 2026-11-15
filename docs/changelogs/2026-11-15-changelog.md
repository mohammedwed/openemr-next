# Changelog 2026-11-15

## Summary

A grounded architecture-dimension review of `OpenEmrNext.PatientManagement.Api` (all 19 files), the
last of the four Patient Management projects reviewed this way (Domain, Application/Application.Ports,
Infrastructure, and now Api). Three findings, all fixed.

## Fixed — three Api findings

- **Documentation**: the "caller-supplied identity, not verified against any authenticated session --
  an already-accepted gap inherited from ADR 0048" caveat previously only lived on
  `RegisterPatientRequest.RegisteredBy`. Added the same note to
  `UploadAdvanceDirectiveDocumentRequest.OwnerId`, `SetAdvanceDirectiveStatusRequest.AuthenticatorUserId`,
  and `FhirPatientEndpoints.CreatePatient`'s `registeredBy` query parameter — the same pattern, now
  documented consistently everywhere it appears. Not a new security finding: the lack of
  authentication itself is ADR 0048's own explicit, named deferral.
- **SOLID**: `PatientEndpoints.RegisterPatient` and `FhirPatientEndpoints.CreatePatient` each
  independently constructed a `PatientRegistration` and called `RegisterPatientUseCase.ExecuteAsync` —
  duplicated orchestration across two Api files. Fixed with an additive convenience overload on
  `RegisterPatientUseCase.ExecuteAsync` that accepts raw primitive fields and constructs the
  registration internally, leaving the existing `PatientRegistration`-accepting overload untouched
  (several integration tests use it directly). Each endpoint keeps shaping its own error response
  (REST `ValidationProblem` vs. FHIR `OperationOutcome`) — only the construction call moved.
- **Security (defense-in-depth)**: `FhirProfileValidator.ExtractIfNeeded` extracted FHIR profile
  packages into a fixed, predictable temp-directory path, gated only by a marker file's existence, not
  its content. Fixed by making the cache content-addressed — the destination subdirectory and marker
  are both keyed on the source `.tgz`'s SHA-256 hash, so a changed package version can never be
  silently shadowed by a stale cache, and skipping extraction now requires matching that exact hash.
- Also found and fixed in the same pass: 2 dead `using` statements in `FhirPatientEndpoints.cs`
  (`Domain.Fhir`, `Domain.Fhir.Rules`) that the earlier Application-layer SOLID cleanup round missed.

## Added — DataAnnotations on every Request DTO

- `[Required]`/`[StringLength]`/`[Range]`/`[EmailAddress]` added to all 5 `Contracts/Requests`
  types, enforced by `builder.Services.AddValidation()` (newly wired in
  `PatientManagementApiExtensions.cs`). Every `ErrorMessage` matches the equivalent Domain factory's
  own wording where one exists; new messages were written only for fields with no Domain
  counterpart. Closes the one genuine prior gap: `SetAdvanceDirectiveStatusRequest.AuthenticatorUserId`
  had no validation anywhere before this. Covered by a new integration test,
  `PUT_status_with_a_non_positive_authenticator_id_returns_400`.

## Cleaned up — Api code organization

- New `ValidationProblems` helper (`Endpoints/ValidationProblems.cs`) replaces 8 duplicated inline
  `Results.ValidationProblem(...)` constructions across `PatientEndpoints`, `InsuranceEndpoints`, and
  `AdvanceDirectiveEndpoints`.
- Removed `FhirPatientMapper`'s duplicate `UsCoreProfile` constant in favor of the existing
  `FhirProfileValidator.UsCorePatientProfile`.
- Removed 4 more dead `using` statements (`Domain.Insurance.Rules`, `Domain.Insurance.ReadModels`,
  `Domain.AdvanceDirectives.ReadModels`, and a bare `Domain` in `FhirPatientEndpoints.cs`) that the
  earlier SOLID cleanup round missed; each verified safe by removal + full rebuild.

## Audited — hardcoded values across PatientManagement

- Swept `Domain`, `Application`, `Application.Ports`, `Infrastructure`, `Composition`, and `Api` for
  literals that should be configuration. Everything found was already a bound options default or a
  legacy-grounded Domain invariant with its source cited in-code -- no new findings there.
- One real finding, left open for a decision: `new MariaDbServerVersion(new Version(12, 3, 3))` is
  duplicated identically in `PatientManagementApiExtensions.cs` and the test fixture
  `PatientDataContainerFixture.cs` (plus a third, independent copy in AuthFacade, out of this
  service's scope). Already acknowledged in-code as a deliberate repo-wide pin, not an oversight --
  not fixed here pending a decision on whether to deduplicate the two `PatientManagement` copies.

## Added — shared-secret gate on PatientManagement.Api (ADR 0055)

- Every endpoint now requires the `X-Internal-Gateway-Secret` header to match a configured secret
  (`InternalGatewayOptions.Secret`), enforced by a new `InternalGatewayAuthenticationHandler` (modeled
  on the deleted `OpenEmrBearerHandler`, ADR 0035) wired via `.RequireAuthorization()` on both route
  groups. A request without it, or with the wrong value, gets 401.
- This closes the "anyone can call this service" half of the gap ADR 0048 named. The other half --
  `RegisteredBy`/`OwnerId`/`AuthenticatorUserId` still being caller-supplied, not resolved from a real
  authenticated identity -- is unchanged and not fixed here. Doing that requires resolving OpenEMR's
  UUID `sub` claim to a numeric `users.id`, which nothing in this codebase does today; ADR 0055 tracks
  this explicitly as a follow-up rather than leaving it undocumented.
- All four HTTP-pipeline integration test fixtures (`PatientEndpointTests`, `FhirPatientEndpointTests`,
  `InsuranceEndpointTests`, `AdvanceDirectiveEndpointTests`) now configure a test secret and attach it
  to every request. New `GatewaySecretAuthenticationTests` proves the gate itself: no header or the
  wrong header both return 401.

## Fixed — PatientManagement's OpenAPI document (ADR 0058)

- The document declared no security scheme at all, despite every endpoint now requiring the
  gateway secret (ADR 0055). Fixed with `PatientManagementOpenApi.ConfigurePatientManagement()`
  (mirrors AuthFacade's own `AuthFacadeOpenApi`): declares `X-Internal-Gateway-Secret` as an
  `apiKey`/header scheme and requires it on every operation.
- DataAnnotations never reached the generated schema (`RegisterPatientRequest` showed no
  `minLength`/`maxLength`/`format`/`minimum` despite real attributes). Root cause: these are
  positional records, so the attributes live on the constructor parameter, not the property
  `JsonPropertyInfo.AttributeProvider` reflects. Fixed with a new
  `ValidationAttributesSchemaTransformer` that also checks `AssociatedParameter.AttributeProvider`.
- Found and fixed along the way: `[Range(1, long.MaxValue)]`'s `Maximum` was writing as a
  precision-losing `9.223372036854776E+18`, not the real `9223372036854775807` — `RangeAttribute`
  has no `(long, long)` constructor, so the bound is already a lossy double inside the attribute
  itself. Since every `[Range]` here is a "must be positive" idiom, the fix omits the wrong
  maximum rather than publish it.
- Covered by a new `OpenApiDocumentTests`, verified against the real generated document.

## Added — a typed PatientManagement client via NSwag (ADR 0059)

- New `OpenEmrNext.PatientManagement.Client` project, same shape as `OpenEmrNext.AuthFacade.Client`
  (ADR 0031): an `OpenApiReference` against a committed snapshot, no `ProjectReference` to the Api
  project.
- New `scripts/dev/generate-patientmanagement-openapi.sh` regenerates the snapshot by running the
  real Api briefly with placeholder config. Unlike AuthFacade's script, it polls `/openapi/v1.json`
  itself for readiness — this Api has no `/health` endpoint.
- Added to `OpenEMR-Next.slnx`. Nothing consumes the generated client yet.

## Added — PatientManagement is deployed and reachable through the Gateway (ADR 0060)

- New `src/Services/PatientManagement/Dockerfile` and `.dockerignore`. The missing `.dockerignore`
  caused a real, confusing build failure (`NETSDK1064`, package "not found" despite a successful
  restore) — the repo-root one only covers WebApp's build, not a per-service one like this; fixed by
  adding PatientManagement's own, mirroring AuthFacade's.
- New `patient-management` docker-compose service (no published host port — only the Gateway may
  reach it), a new `INTERNAL_GATEWAY_SECRET` shared between it and the `gateway` service
  (`.env.example`, `new-env.sh`, CI), and `patient-management` added to CI's `docker compose up`
  service list.
- Gateway (`Program.cs`): a new `patientmanagement` cluster, two routes
  (`/api/v1/patients/{**catch-all}`, `/fhir/{**catch-all}`), and a request transform scoped to only
  that cluster attaching `X-Internal-Gateway-Secret` unconditionally. Two new `GatewayStackTests`
  prove this against the real running stack.
- New `GET /api/v1/patients` endpoint, reusing `SearchPatientFhirRecordsUseCase` as-is — no new
  Domain/persistence work — since the only existing search path returns a FHIR `Bundle` with no
  typed NSwag model. Covered by a new integration test.
- WebApp: `IPatientManagementClient` registered alongside `IAuthFacadeClient`, each with its own
  `HttpClient`/circuit breaker (renamed `AuthFacadeCircuitBreakingHandler` →
  `TransientFailureCircuitBreakingHandler`). Two new pages (`Pages/Patients/Index.razor`,
  `Register.razor`), enabling the previously-disabled "Patients" nav link. Resolved a real
  `ApiException` namespace collision between the two generated clients with explicit `@using`
  aliases.
- Verified against the real running `docker compose` stack: `docker compose up` with
  `patient-management` included, all services healthy, Gateway integration tests passing against it,
  and a direct `curl` through the gateway (no secret presented by the caller) registering a patient
  and listing it back.

## Added — WebApp now covers insurance and advance directives (ADR 0061)

- `PatientFhirView`/`PatientSummaryResponse` gain `Pid`; new `GET /api/v1/patients/{uuid}` (reuses
  `GetPatientFhirRecordUseCase`) — needed because insurance/advance-directive routes are keyed by
  `pid`, which the patient list never exposed before.
- New `Pages/Patients/Detail.razor`: Insurance (effective coverage, add policy, swap) and Advance
  Directives (status, document list/download, upload) sections, both reached from the patient list.
- **Three real bugs found only by exercising the real deployed stack**, all fixed:
  - Document download bypasses the generated client (bodyless — NSwag can't type a per-document
    content-type) in favor of a direct link to the Gateway's relative URL.
  - `UnauthorizedAccessException` on upload: `sitesvolume`'s `documents` directory is uid-1000-only
    (mode `700`, matching OpenEMR's own `apache` worker uid). Fixed with `user: "1000:1000"` on the
    `patient-management` service in `docker-compose.yml`.
  - `SELECT command denied... 'sequences'` on upload, after the uid fix: the seed script granted only
    `UPDATE` on `sequences`, which MariaDB doesn't treat as sufficient for a `SET id = LAST_INSERT_ID(id
    + 1)` expression. Fixed by granting `SELECT, UPDATE`. Also surfaces a real test gap:
    `PatientDataContainerFixture` never exercises the real least-privilege grant set at all (connects
    with a fully-privileged user) — tracked as a separate follow-up, not fixed here.
- Verified end to end against the real running stack for every feature via `curl` through the
  Gateway: insurance add/swap/effective-coverage, advance-directive status, and document
  upload/list/download (byte-for-byte correct) all round-trip correctly.

## Tests

Full solution builds clean (0 errors, 0 warnings across every project touched today). Full
PatientManagement suite passing: 57 integration tests (including the new `Pid`/single-patient-lookup
tests), 121 unit tests, 17 architecture tests. Gateway's integration suite passing against the real
stack: 7 tests.

## Added — WebApp UI/UX quick wins

- **Toast feedback**: `MudSnackbarProvider`/`MudDialogProvider` added to `App.razor` (outside the
  `Router`, so a toast survives the navigation a successful action often triggers). Every
  insurance/advance-directive action (add policy, swap, set status, upload) and patient registration
  now shows a success snackbar — previously the only feedback was the list silently refreshing.
- **Confirmation before an irreversible action**: swapping an insurance policy's level now asks first
  (`IDialogService.ShowMessageBoxAsync`) — the one action here that displaces whatever already
  occupies the target level, unlike adding a policy or setting a status.
- **Real validation messages, not a flat "something went wrong"**: a new
  `Shared/PatientManagementErrorFormatter.cs` reads the typed `ApiException<HttpValidationProblemDetails>`
  the generated client already throws for a 400 and surfaces the actual message(s) the Api returned,
  falling back to a generic message only for anything else (a 500, a network failure). Wired into
  every catch block across `Register.razor`/`Detail.razor`.
- **A working search box on the patient list**: the backend's `?name=` filter was already there with
  no way to reach it from the UI. `Index.razor` gets a search field (button or Enter-triggered, no
  debounce needed for a quick win) that re-queries `GetPatientsAsync` with the typed term.

Verified: full solution still builds with 0 warnings; redeployed `webapp` on the real running stack
and confirmed `/patients` still serves correctly; Gateway's integration suite (7 tests) and
PatientManagement's full suite (57/121/17) still pass unchanged, since none of this touched the Api.

## Fixed — WebApp's Material Design was running on bare defaults

- `<MudThemeProvider />` had no `Theme=`, so the app ran MudBlazor's own stock palette rather than a
  deliberately chosen one. New `Theme/OpenEmrNextTheme.cs` sets Primary to OpenEMR's own brand purple
  (`#5c2d91` -- the dominant color already baked into `wwwroot/icon-192.png`, confirmed by sampling the
  icon's actual pixels, not guessed) and a contrasting teal Secondary, with lighter dark-mode variants
  of each.
- `Detail.razor`'s `MudExpansionPanels` had no explicit `Elevation`, unlike every other content
  surface in the app (`Elevation="2"`, matching `MudPaper` elsewhere). Added for consistency.
- Verified visually against the real running stack (Chrome): the app bar, active nav highlight, and
  "Add policy"/"Swap" buttons now show the brand purple/teal distinctly, and both expansion panels
  (Insurance, Advance directives) now have the same visible elevation as every other card in the app.

## Added — main layout adjustments

- **A user menu in the app bar**: shows who's signed in and a "Log out" link, where previously only
  `Home.razor` had any logout affordance at all. Falls back to a shortened subject id when `Name` is
  empty (OpenEMR's tokens in this stack carry no name claim -- the same known gap `Home.razor`'s own
  display already has, not a new one).
- **A collapsible drawer**: a hamburger toggle in the app bar now opens/closes the nav drawer
  (`@bind-Open`), where it was previously always persistently open with no way to reclaim the space.
- **Breadcrumbs reflecting the current page**: `MudBreadcrumbs`, driven by a plain path-segment match
  against `NavigationManager`'s current URI (re-evaluated on `LocationChanged`) -- not a per-page
  cascading value, since this app has few enough routes that the extra plumbing isn't worth it yet.
- Verified visually against the real running stack (Chrome): the menu opens and shows "Signed in as
  &lt;subject&gt;" / "Log out", the drawer toggles correctly and the main content reclaims the freed
  space, and breadcrumbs show correctly on `/patients/register` ("Patients / Register").

## Fixed — every page's top content was rendering hidden behind the app bar

- **Found while investigating a UI complaint**: `MudMainContent`'s `Class="pa-4"` sets
  `padding: 16px !important`, which overrides MudBlazor's own built-in `padding-top: calc(var(--mud-
  appbar-height) - ...)` rule -- the one that clears the fixed 64px app bar. With `pa-4`, every page's
  top ~40px of content rendered underneath the app bar, invisible, while the rest of the page below it
  rendered normally -- explaining why `/patients`' own "Patients" heading and "Register patient"
  button, and `Home.razor`'s "Dashboard" heading, never actually appeared, despite existing correctly
  in the DOM the whole time (confirmed via the actual computed styles and matched CSS rules in a
  running browser, not guessed). This bug predates every round of frontend work this session -- it was
  already present in `MainLayout.razor`'s very first version, not something introduced recently.
- Fixed by changing `MudMainContent`'s class to `px-4 pb-4` (horizontal and bottom padding only),
  leaving `padding-top` untouched so MudBlazor's own appbar-clearing rule applies.
- Verified visually against the real running stack (Chrome, with a hard reload to rule out caching):
  `/patients`' heading and button, and `Home.razor`'s heading, now render fully visible below the app
  bar on every page.
