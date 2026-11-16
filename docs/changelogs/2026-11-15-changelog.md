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

## Changed — WebApp's chrome flattens toward the plain Blazor template's look (ADR 0062)

- Even after the app-bar fix above, the UI still read as heavier/more "branded" than a plain
  `dotnet new blazor` scaffold. Asked what specifically felt that way, the answer covered all three
  axes: visual weight (shadows/elevation), nav structure (a persistent sidebar), and color (a solid
  saturated purple app bar).
- **Dropped the persistent `MudDrawer` sidebar** for a flat inline top nav (`NavLink`s inside the app
  bar itself), styled with the active route in purple text + underline and everything else muted gray.
  Breadcrumbs moved below the app bar into `MudMainContent`.
- **Neutralized the app bar's color**: `OpenEmrNextTheme` now sets `AppbarBackground`/`AppbarText`
  explicitly (white / dark text in light mode) instead of letting MudBlazor's default paint the whole
  bar with `Primary` — purple now shows up only as an accent (active nav, primary buttons), not as a
  solid block.
- **Reduced elevation/shadow use**: app bar and nav now `Elevation="0"` with a hairline border instead
  of a shadow; cards/tables that were `Elevation="2"` are now `1`; alerts switched from filled to
  `Variant="Outlined"` + `Dense`; the patient detail page's two feature sections moved from one shadowed
  `MudExpansionPanels` to two separate flat `MudPaper` panels.
- **Found and fixed two real bugs while making this change, neither caught by the build**:
  1. Blazor's CSS-isolation scope attribute is never added to `NavLink`'s own rendered `<a>` — a first
     attempt at scoped `MainLayout.razor.css` nav styles silently matched nothing (confirmed via the
     live DOM: no `b-xxxxxxxx` attribute on the anchor at all). Fixed by moving the nav styles into the
     existing global `wwwroot/css/app.css` instead.
  2. `Color="Color.Secondary"` on `MudText` was being used to mean "muted gray text," but it actually
     maps to the theme's teal accent, not the muted `TextSecondary` palette token — so several
     supporting lines (the patient detail page's DOB/Pid line, sign-in labels, empty-state captions)
     rendered in loud teal instead of quiet gray, undermining the very goal the change was meant to
     serve. Fixed by switching those to `Class="mud-text-secondary"`, confirmed against the installed
     package's own compiled CSS. The one legitimate accent use (the insurance "Swap" button) was left
     unchanged.
- Verified visually against the real running stack (Chrome, hard reload): active nav link renders in
  purple with an underline, inactive links in muted gray, "Encounters" visibly dimmed as disabled; the
  app bar is flat and white; the patient detail page's DOB/Pid line and other supporting text render in
  muted gray, not teal.
- **Follow-up round**: still "not elegant" with no further specifics given, so the page was inspected
  directly instead of asking a fourth round of categorization questions. Two more concrete defects
  found and fixed:
  1. The page canvas (`Background`) and card surface (`Surface`) were both pure white — confirmed via
     `getComputedStyle` — so a card's only separation from the page was a barely-visible shadow. Fixed
     by giving `Background` a faint neutral tint (`#f7f7f8`), leaving cards white.
  2. "Dashboard"/"Patients" appeared three times stacked within ~100px (active nav link, a pointless
     single-item breadcrumb, and the page's own heading). Fixed by only rendering breadcrumbs when
     there's an actual 2+ item trail (e.g. "Patients / Register").
  3. `MudDatePicker` fields were missing `Variant="Variant.Outlined"`, rendering underlined while every
     other field on the same form was outlined. Fixed across `Register.razor` and `Detail.razor`.
- Verified visually again (Chrome, hard reload): cards now visibly separate from the page canvas,
  top-level pages ("Dashboard", "Patients") no longer show a redundant breadcrumb, nested pages
  ("Patients / Register") still do, and every date field matches the outlined style of its neighbors.

## Fixed — MainLayout's chrome had no mobile-width fallback and two accessibility gaps

- **No responsive fallback for the top nav below the `sm` breakpoint (600px)**: the flat inline nav
  introduced by ADR 0062 has nowhere to go at phone width — nothing in that ADR considered viewport
  size, only route count (it explicitly accepted not scaling past "a handful of routes," with "revisit
  if the route list grows" as the named trigger for *that* tradeoff). This is a different gap, not a
  reopening of that one. Fixed with `op-nav--desktop`/`op-nav--mobile`: the same three links render
  twice, swapped by a `max-width: 599.98px` media query in `app.css` — a hamburger `MudMenu` below the
  breakpoint, the existing inline nav above it. Deliberately plain CSS, not `MudHidden`: `MudHidden`
  decides via a JS viewport observer, so it can't apply until after a JS-interop round trip, meaning the
  wrong nav would flash on first paint every time; a media query is correct from first render.
- **The disabled "Encounters" placeholder was unreachable except by mouse hover**: a non-focusable
  `<span>` behind a `MudTooltip` that only bound to `mouseenter`. Touch devices have no hover state and
  keyboard users had no way to tab to it, so both got a dimmed label with zero explanation. Fixed with
  `tabindex="0"`/`role="link"`/`aria-disabled="true"` on the span (reachable and announced) and
  `ShowOnClick="true"` on the tooltip (covers touch taps).
- **Both icon-only `MudMenu` triggers (the new hamburger, and the existing user-account menu) had no
  accessible name** — screen readers get nothing to announce beyond "button." Added `AriaLabel` to
  each.
- **Found while reviewing the fix, not the original finding**: the user-menu slot pops into existence
  only after `GetCurrentUserAsync()` resolves, visibly shifting the app bar's contents for a signed-in
  user. Fixed with a `_loadingUser` flag and a fixed-size placeholder (`.op-user-slot-placeholder`)
  shown only while that call is in flight; a signed-out visitor still sees nothing there afterward, same
  as before.
- **Not touched, and not new findings**: the breadcrumb path-segment `switch` and the UUID-fragment
  `_userLabel` fallback are both already-flagged, intentional stopgaps from the prior author's own
  comments in this file — left exactly as they were.
- Verified against a standalone `dotnet run` instance (Chrome): real window resizing has no effect in
  this sandbox (confirmed — `resize_window` and `window.resizeTo` both left `window.innerWidth`
  unchanged), so the breakpoint was exercised with a same-origin `<iframe>` sized to 375×700 — each
  frame's own document evaluates `@media` against its own viewport, so this is a faithful test of the
  real breakpoint, not a simulation. At 375px: the inline nav disappears, the hamburger appears, opening
  it lists Dashboard/Patients/"Encounters (coming soon)" correctly, and clicking "Patients" navigates
  correctly inside the frame. At the outer 1280px window: the inline nav renders as before, Tab reaches
  "Encounters" and shows its focus ring with the "Coming soon" tooltip visible. The user-menu placeholder
  size (48×48px) was measured directly via `getBoundingClientRect()` on the real hamburger button rather
  than assumed from `MudIconButton`'s compiled CSS rules alone, which don't by themselves guarantee the
  icon glyph's rendered box exactly equals its `font-size`. Full solution builds clean (0 errors, 0
  warnings).
- **Follow-up checks, both clean**: (1) Dark mode — `MudThemeProvider.ObserveSystemDarkModeChange`
  defaults to `true`, so the app already follows the OS/browser preference with no app code involved;
  verified by temporarily hardcoding `IsDarkMode="true"` on `App.razor`'s `<MudThemeProvider>` (reverted
  before committing, never shipped) and checking the desktop nav, mobile hamburger menu, and the
  "Encounters" focus ring/tooltip all render with correct contrast against the dark palette. (2) The
  narrowest common phone width (320px, e.g. iPhone SE) — the brand text and hamburger fit with ~75px of
  slack to spare, more than the 48px the account-menu icon would also need once signed in.
- **Reported against the real docker-compose stack**: the hamburger sat dead-center in the app bar on
  mobile, floating between the brand and the account icon rather than grouped with either — it had
  simply been dropped into the slot the desktop nav vacates (between the same two `MudSpacer`s), without
  reconsidering where a mobile app bar's action controls conventionally cluster. Fixed by moving it after
  the second spacer, immediately beside the account-menu slot: with the desktop nav hidden on mobile, the
  two spacers collapse into one continuous gap, pinning the brand left and the hamburger+account cluster
  right, with a small explicit `margin-inline-end` between them so they read as two controls, not one
  wide target. Verified the same way as the position check above — real window resizing has no effect in
  this sandbox, so a same-origin iframe was used, with `OnInitializedAsync` temporarily short-circuited to
  a hardcoded signed-in state (reverted before committing, never shipped) since the standalone instance
  has no backend to authenticate against and the account icon only renders when signed in.
- **Cleanup pass requested on this file**: two refactors, no behavior change.
  1. `_navItems`: the app bar's two real routes were hand-written twice (desktop `NavLink`s, mobile
     `MudMenuItem`s) — a drift risk the moment a third route goes live, which ADR 0062 already names as
     the trigger to revisit this nav. Extracted to one `(Label, Href, ExactMatch)[]`, rendered into both
     surfaces via `@foreach`. The disabled "Encounters" placeholder stays hand-written in each — the two
     surfaces need genuinely different treatment for it (a focusable tooltip-bearing span vs. a plain
     disabled item with the note folded into its label), so sharing it would force one surface into the
     wrong shape.
  2. `_userState`: replaced the `_loadingUser` bool + nullable `_userLabel` pair (three valid states
     encoded across two fields) with a `Loading`/`SignedIn`/`SignedOut` enum — the same shape
     `Home.razor` and `Patients/Index.razor` already use for this exact `GetCurrentUserAsync` check,
     applied here for consistency rather than introduced as something new.
  - Verified against the docker-compose stack: `Patients` still shows active across `/patients` and its
    sub-routes, the mobile menu still lists Dashboard/Patients/"Encounters (coming soon)" correctly, and
    the account menu still shows "Signed in as …"/"Log out" when signed in. Full solution still builds
    clean (0 errors, 0 warnings).

## Changed — aesthetics pass on the nav and both app bar menus

- **Discoverability of the disabled "Encounters" placeholder, on both surfaces**: previously a hover-only
  tooltip (desktop) or a parenthetical in the label (mobile). Replaced on both with an always-visible
  small "Soon" badge (`.op-soon-badge` — a neutral mid-gray pill, not a theme color, so it stays muted
  without picking a side between light/dark palettes) — no hover, focus, or tap needed to learn it's
  unavailable. Desktop's disabled span keeps `aria-label="Encounters, coming soon"` for a clean screen
  reader announcement instead of relying on the (removed) tooltip.
- **The mobile dropdown had no sense of "you are here"**: a plain list of links, unlike the desktop nav's
  purple active-state text + underline. Added a `IsActiveRoute` check (the same ExactMatch-vs-prefix
  logic `NavLink` already applies, done by hand since `MudMenuItem` has no built-in equivalent) driving
  `op-menu-item-active` (primary-colored, bold) on the current route's item and its icon color.
- **Icons added to the mobile nav and account menus** (`Icons.Material.Filled.Dashboard`/`People` on the
  two real routes, `EventNote` on the disabled placeholder, `Logout` on "Log out") — folded into
  `_navItems` as a fourth field rather than a separate lookup, so icon and route stay defined together.
  The desktop inline nav stays icon-free deliberately — ADR 0062 chose that plain-text look on purpose,
  and this pass doesn't reopen it.
- **The account menu's "Signed in as …" header looked like a disabled, broken button**, not a label —
  it was a `Disabled="true"` `MudMenuItem`, a style meant for an action that can't be taken right now,
  misapplied to a line of text that was never an action. Replaced with a plain two-line block (`MudText`
  caption + bold name), padded to match the real menu items' own rhythm, confirmed against the running
  instance rather than assumed from the compiled CSS alone.
- Verified in both light and dark mode (the same temporary `IsDarkMode="true"` + hardcoded signed-in
  override used earlier in this file's history, reverted before committing, never shipped): the badge,
  active-item color, icons, and header text all hold legible contrast in both. Full solution builds
  clean (0 errors, 0 warnings).

## Changed — two more menu/nav polish items, requested after the aesthetics pass above

- **Initials avatar replaces the generic AccountCircle icon** on the account menu trigger: this chrome
  only ever shows the current viewer's own identity, never a roster of other users to tell apart, so a
  single fixed brand color (not a per-user hash-to-color scheme) is enough. `GetInitials` takes one
  letter from each of the first two words for a real name, or the first two characters of a single token
  (today's UUID-fragment fallback), so it's never a single letter wide.
- **The mobile nav trigger now morphs hamburger<->close (`@bind-Open` driving the icon and its
  `AriaLabel`)** — the standard "tap again to close" affordance, previously static regardless of state.
- **A real accessibility bug found and fixed while wiring the avatar in**: `MudMenu.AriaLabel` is
  documented as a no-op once `ActivatorContent` is set (its own XML doc comment says so), silently
  dropping the "Open user menu" label the old `Icon=`-based activator carried. A first fix attempt —
  `aria-label` directly on `MudAvatar`'s own root — didn't bubble up to the clickable wrapper's computed
  name either, confirmed via Chrome's own accessibility tree (the button showed no name; only its
  `aria-hidden` child did). Fixed with the standard bulletproof pattern instead: a visually-hidden
  (`.op-visually-hidden`, the usual clip-based "sr-only" rule) literal text node alongside the
  `aria-hidden` avatar — "name from content" for a `role=button` concatenates real rendered text
  regardless of which internal element an attribute happened to land on.
- **The avatar's default `MudAvatar` size (40px) didn't match the 48px hamburger it now sits beside** —
  measured via `getBoundingClientRect()`, same as the hamburger's own 48px figure recorded earlier in
  this file's history. Sized up to 48px (`.op-user-avatar`) so both app bar controls read as the same
  touch-target weight; the existing 48px loading placeholder needed no change.
- Verified against a standalone instance (light and dark, desktop and the same same-origin-iframe mobile
  technique used throughout this file's history): the avatar and hamburger now align exactly, the
  hamburger icon visibly morphs to an X when the mobile menu opens and back when it closes, and the
  account button's computed accessible name was confirmed via Chrome's own accessibility tree (not
  assumed) to carry "Open user menu" the same structural way the pre-existing, known-working "Register
  patient" button already does. Full solution builds clean (0 errors, 0 warnings).

## Fixed — a real bug: the account menu stopped opening entirely, plus two sizing issues

- **Reported as "the menu feels a bit shrunk."** Investigating turned up one correctness bug and two
  genuine sizing issues, all in the same account-menu code from the round above.
- **The account menu could no longer be opened at all, by click or otherwise** -- `MudAvatar` inside
  `ActivatorContent` rendered fine but nothing happened on click. A long bisection chased several wrong
  leads first (a screenshot-vs-viewport coordinate scale mismatch in how this file's own testing clicks
  had been issued, and a stale-WASM-cache theory, both ruled out by testing with `getBoundingClientRect`-
  measured coordinates and a fully cache-cleared fresh tab) before finding the real cause in MudMenu's
  own XML doc comment: the built-in activator (`Icon=`/`Label=`) wires its own click handler
  automatically, but a custom `ActivatorContent` does not -- the docs say explicitly that a click
  activator "should call the provided `MenuContext`," which this code never did. Fixed by giving
  `ActivatorContent` an explicit `Context="menuContext"` and adding `@onclick="menuContext.ToggleAsync"`
  to the `MudAvatar`. Confirmed working with a real click in a fresh tab, open and close both.
- **The popover itself was only ~115px wide** -- MudMenu has no min-width of its own, so a short label
  set ("Dashboard"/"Patients", or "Signed in as"/"Log out") left it hugging content tightly. Measured via
  `getBoundingClientRect()` on the real popover, not guessed. Fixed with `PopoverClass="op-menu-popover"`
  on both app-bar menus and a new `.op-menu-popover { min-width: 200px; }` rule.
- **The account menu's custom header block had half the real menu items' own vertical padding** -- an
  earlier commit's `.op-menu-header { padding: 4px 16px; }` was asserted as "measured against the real
  menu" but wasn't actually verified at the time; measuring it now against the real "Log out" item's
  computed style showed `8px 12px`, not `4px 16px` -- wrong on both axes. Fixed to match exactly.
- The hamburger<->close icon morph (`@bind-Open`), briefly reverted purely to isolate the click bug above
  from an unrelated sibling component, is restored and still works correctly.
- Verified end to end against a standalone instance, then the real docker-compose stack: the account menu
  opens and closes on a real click, both popovers are visibly wider, the header's spacing now matches the
  menu items below it, and the hamburger still morphs to an X when open. Full solution builds clean
  (0 errors, 0 warnings).

## Changed — the nav drawer is back (ADR 0063, supersedes ADR 0062's nav-structure decision)

- **Asked directly, after being shown the tradeoff**: restoring the drawer reopens 0062's nav-structure
  call, whose own named trigger ("the route list grows materially") hasn't fired. That was surfaced back
  before doing the work; the user chose to proceed anyway, so this is a deliberate reversal, recorded as
  [ADR 0063](../architecture/0063-webapp-restores-the-nav-drawer.md), not a quiet one. 0062's other two
  decisions (the app bar's flat, neutral color and reduced elevation) are untouched -- only the
  nav-structure call is superseded.
- **`MudDrawer Variant="DrawerVariant.Responsive"` replaces both the flat inline nav and the hamburger
  dropdown it had grown** to cover mobile widths 0062 never considered. One `MudNavMenu` now feeds
  `MudNavLink`s from the same `_navItems` source, at `Breakpoint="Breakpoint.Md"` (960px, the library's
  own default) -- deliberately not the 600px breakpoint the removed dropdown used: a persistent side rail
  competing with page content for width warrants collapsing to an overlay at a wider point than a top nav
  row did.
- **The drawer toggle button replaces the hamburger menu** at the app bar's start (`Edge.Start`, matching
  the original pre-0062 toggle's position), keeping the hamburger<->close icon morph the dropdown had
  added, now reflecting the drawer's own open state.
- **Everything added to the nav since 0062 shipped carried over, not just reverted away**: the "Soon"
  badge and real accessible name on the disabled "Encounters" placeholder, now inside `MudNavLink`'s own
  `Disabled` state -- which already covers the focusable/announced-as-disabled concerns the flat nav's
  hand-rolled `tabindex`/`role`/`aria-disabled` trio existed to work around. That trio, and
  `IsActiveRoute` (`MudNavLink` computes its own active state the same way `NavLink` already did), are
  gone as dead code along with the flat nav and dropdown that needed them.
- The restored drawer gets the same `Elevation="0"` + hairline-border treatment the app bar already uses
  (`.op-drawer { border-right: 1px solid var(--mud-palette-lines-default); }`), not the pre-0062 drawer's
  `Elevation="1"` shadow -- 0062's flat/neutral visual language was never in question here.
- Verified against a standalone instance: the drawer opens persistent-and-pushing-content at desktop
  width, collapses to a hamburger-triggered temporary overlay with a working backdrop-click-to-close
  below `Breakpoint.Md`, the toggle button's icon morphs correctly both ways, "Patients" shows active in
  purple with its icon matching, and the account-menu avatar (untouched by this change) still opens and
  closes correctly. Checked in dark mode too: legible throughout; the drawer's background reading darker
  than the page canvas there is inherited from MudBlazor's own stock dark `Surface` value, unrelated to
  this change. Full solution builds clean (0 errors, 0 warnings).

## Added — an explicit dark-mode toggle

- Until now, dark mode only followed the OS/browser's `prefers-color-scheme` (`MudThemeProvider`'s own
  `ObserveSystemDarkModeChange`, true by default) -- no in-app control existed to pick one independent of
  the system. Added a sun/moon `MudIconButton` in the app bar, grouped with the account control rather
  than the drawer toggle, since this is a per-viewer preference, not part of the nav structure.
- New `ThemeService` (`Theme/ThemeService.cs`, registered `AddScoped` alongside the app's other services
  in `Program.cs`), shared between `App.razor` (hosts `MudThemeProvider`) and `MainLayout.razor` (hosts
  the toggle button) since they're different components: holds `IsDarkMode`, persists an explicit choice
  to `localStorage` (`op-dark-mode`), and exposes `HasExplicitPreference` so `App.razor` can turn
  `ObserveSystemDarkModeChange` off once the user has picked one -- otherwise a later system theme change
  would silently override what they just chose in this app.
- **`App.razor` now gates its entire render on an async `LoadAsync` localStorage read finishing first**,
  rather than rendering immediately with defaults and swapping `ObserveSystemDarkModeChange` in after:
  that parameter isn't documented to support being toggled after `MudThemeProvider`'s first render, so
  this guarantees it's already correct then instead of relying on undocumented reactive behavior. The
  cost is a brief blank frame while the (near-instant) localStorage round trip resolves.
- `IsDarkMode`/`IsDarkModeChanged` are wired explicitly, not via `@bind-IsDarkMode`: binding would blindly
  reassign `ThemeService.IsDarkMode` on every change `MudThemeProvider` raises, including system-
  preference changes after an explicit choice already exists -- `OnSystemDarkModeChanged` only accepts
  those while `HasExplicitPreference` is still unset.
- Verified against a standalone instance: toggling switches the whole app instantly both directions, a
  full page reload after choosing dark loads straight into dark mode with no flash of light mode first,
  and the icon correctly shows which mode a click would switch *to* (moon in light mode, sun in dark).
  Checked that a fourth app-bar control doesn't crowd the narrowest common phone width (320px): drawer
  toggle, brand, dark-mode toggle, and the account avatar all fit without overlap, verified via a signed-
  in test. Full solution builds clean (0 errors, 0 warnings).

## Changed — the restored drawer is no longer dismissable on desktop/tablet

- Asked directly: the drawer shouldn't be closeable. Clarified which scope first, since a flat "always,
  on every screen size" reading would remove mobile's only way to reach navigation at all (nowhere for a
  persistent drawer and page content to both fit on a phone) -- confirmed desktop/tablet only, mobile
  keeps today's toggle.
- `.op-drawer-toggle` hides the toggle button at/above the drawer's own `Breakpoint.Md` (960px, confirmed
  against the library's own XML doc remarks for the `Md` breakpoint). Below it, unchanged.
- **Checked empirically, not assumed, before relying on it**: does `DrawerVariant.Responsive` honor
  `Open="false"` above its own breakpoint, or does it force itself open there regardless? Temporarily
  defaulting `_drawerOpen` to `false` and loading at desktop width showed the drawer genuinely collapses
  -- it does honor `Open`. That matters here: hiding the toggle alone would leave a dead end if the
  drawer was ever closed while the window was narrow and then grown past the breakpoint without a full
  reload, since there'd be no visible control left to reopen it.
- Added that missing half as a safety net: `MainLayout` now injects MudBlazor's `IBrowserViewportService`
  and subscribes to live breakpoint changes (`OnViewportChanged`), forcing `_drawerOpen` back to `true`
  the moment the viewport is `Md`/`Lg`/`Xl`/`Xxl` and it was found `false`. `MainLayout` moved from
  `IDisposable` to `IAsyncDisposable` to unsubscribe this alongside the existing `NavigationManager`
  handler.
- Verified against a standalone instance: the toggle is gone at desktop width with no way to close the
  drawer there; at mobile width it's still present and the drawer still opens/closes/backdrop-dismisses
  exactly as before; and the safety net itself was exercised directly, not inferred -- closing the drawer
  in a narrow same-origin iframe, then growing that same iframe past 960px without a reload, reopened the
  drawer and hid the toggle automatically. Full solution builds clean (0 errors, 0 warnings).
