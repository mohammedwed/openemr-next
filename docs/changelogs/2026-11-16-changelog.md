# Changelog 2026-11-16

## Changed — patient view is read-only by default; changes happen in dialogs

- Previously every panel showed its records and all of its edit forms at once, so a viewer could not
  tell whether they were looking or changing something. Both tabs are now read-only lists with action
  buttons.
- **Add policy**, **Swap levels**, **Set status**, and **Upload document** each open a dialog that makes
  one change. A dialog closes only when its save succeeds. On failure it stays open with the typed values.
- **Swap levels** names the policy being moved and whoever holds the target level before the user
  confirms, and its button says what it does ("Swap levels").
- Dialog components: `AddPolicyDialog`, `SwapPolicyDialog`, `SetAdvanceDirectiveStatusDialog`,
  `UploadDocumentDialog` (all under `Pages/Patients`).
- Verified in the browser by opening and cancelling each dialog, and confirming that Upload stays disabled
  until a file is chosen. **Not verified:** a successful submit of any dialog, since that would change the
  dev database. Submitting still needs a check.

## Changed — patient view split into panels

- `Pages/Patients/Detail.razor` is now the header, the states, and the tabs. Its two sections moved
  into `PatientInsurancePanel.razor` and `PatientAdvanceDirectivesPanel.razor`. Each takes the patient's
  `Pid`, loads its own data, and owns its own forms and errors. Both are keyed on the patient's `Pid`, so
  switching patients rebuilds them instead of carrying state across.
- Each panel shows a spinner until its first load, so it no longer flashes "No effective insurance today"
  before the data arrives. Both panels have "Try again" on a failed load.
- Readable names moved to `Shared/CodeLabels.cs`.
- Verified in the browser: both tabs load their data, the directive status and document show, and moving
  to an unknown patient and back reloads correctly.
- Open: view and edit controls share one screen (see the patient view design note).

## Changed — patient identifier and patient view (webapp and PatientManagement API)

- **API: `PatientSummaryResponse` now includes `PublicPatientId`** (OpenEMR's `pubpid`, the identifier
  its own screens show), mapped from the existing `PatientFhirView.PublicPatientId`. Added as a trailing
  nullable record parameter, so no existing construction site changed. Regenerated
  `OpenAPIs/PatientManagement.json` with `scripts/dev/generate-patientmanagement-openapi.sh`; the diff is
  only the new property. This is not an MRN: the service does not model one.
- **Patient list** shows the public ID. Sorting stays on the internal `Pid`, so numeric IDs sort as
  numbers ("9" before "10").
- **Patient view header** shows "Patient ID: [public ID]" instead of the internal `Pid`. The `Pid` is still
  used for every API call on the page.
- **Route change without reload.** Moving from one patient to another within the page used to keep the
  previous patient's data, because the page loaded only on first render. It now reloads on each route change,
  clears the previous patient's state, and drops any response that arrives for a patient the user has left.
- **Tabs.** Insurance and advance directives are tabs (`MudTabs`) instead of two stacked expansion panels.

## Changed — patient view (`Pages/Patients/Detail.razor`)

- **Retry on load failure.** The error state had no way to recover. "Try again" reloads the patient;
  a 401/403 shows the sign-in prompt, and a network failure or 5xx shows the outage message.
- **Readable codes.** Insurance levels, document types, and the advance-directive status showed raw
  stored values (`living_will`, `dnr_order`, `unknown`). A single lookup now shows "Living will",
  "DNR order", "Unknown", and so on. Unknown codes still show the raw value.
- **Accessible names.** Each document's "Download" link is named for its document and says it opens in a
  new tab. The documents table has a screen-reader label on its actions column, and the file input has a label.
- Verified in the browser: retry after a backend outage, and the readable insurance level and status.
  Document-related labels were checked in the DOM only, because the test patient has no documents.

## Changed — patient list (`Pages/Patients/Index.razor`)

- **Search failures no longer hide the page.** A failed search shows an inline error above the last
  results and keeps the search box usable. The error says when the list is from before the search, so
  stale results are not mistaken for the new term's results. Only a failed first load replaces the page.
- **Accessible names.** Each "View" link is named for its patient ("View [first] [last]"), the actions
  column has a screen-reader label, the table is named "Patients", the spinner is labeled, and the
  result count is a live status region.
- **Sorting.** Patient ID, first name, last name, date of birth, and email sort on header click (second
  click reverses). Headers are real buttons with `aria-sort`, not `MudTableSortLabel`, whose sort control
  is a focusless `<span>` and cannot be reached by keyboard.
- **Pagination.** `MudTablePager`, 10 rows by default, with 10/25/50 page sizes. Paging is client-side,
  over the list the page already loads.
- **Retry on load failure.** A failed first load shows "Try again". A 401/403 from PatientManagement is
  treated as signed out (sign-in prompt); network failures and 5xx responses are treated as an outage
  (retry prompt). Network failures used to escape as unhandled exceptions.
- **Patient ID column.** Shows the OpenEMR internal `Pid`. It does not show the MRN: the API's
  `PatientSummaryResponse` does not expose one.
- **Column list.** The sortable headers are generated from one list, not four copies of the same markup.
- **Width.** Patient list and detail pages use `MaxWidth.Large` (1280px) instead of `Medium` (960px).
- **Shared sign-in check.** `Shared/AuthFacadeClientExtensions.TryGetCurrentUserAsync` is used by the
  list and detail pages. The home page still has its own check (it needs the user's name).

## Known gaps (patient list)

- The patient API (`GET /api/v1/patients`) returns patient data without a user sign-in check. The
  gateway passes the call through with the internal secret; the page's own sign-in check is the only
  gate. This is intended per the owner's decision (ADR 0055's trust model); noted here so it stays visible.
- Not browser-verified: light mode, phone width, moving between pages, sort order with more than one
  patient, and the 401/403 path.
- Row click was tried and removed: it added a second history entry when the "View" link was clicked.
- The generated client's `GetPatientsAsync(string name)` is non-nullable, so the page passes `null!`
  for "no filter". Fixing it means changing the API's OpenAPI output and regenerating the client.

## Fixed — page content had zero breathing room below the app bar, in both themes

- **Reported as "a button bleeding into the navbar," with a screenshot.** First suspected a dark-mode
  contrast issue -- `AppbarBackground` (`#1e1e1e`) is explicitly set darker than the stock
  `Background`/`Surface` MudBlazor falls back to in dark mode (never given the same deliberate tint
  `PaletteLight.Background` got), inverting the normal "surface lighter than canvas" elevation direction
  and making the bar and the page below it read as one undifferentiated dark mass. That's real, but the
  user confirmed the same thing happens in light mode too -- where the bar is plain white against a
  clearly different light-gray canvas, not a subtle dark-on-dark contrast call. That ruled out contrast
  as the cause and pointed at something theme-independent.
- **The real cause, confirmed against MudBlazor's own compiled CSS, not guessed**: `.mud-main-content`'s
  `padding-top` is `calc(var(--mud-appbar-height) - var(--mud-appbar-height)/8)` by default, but a
  `@media (min-width: 600px)` rule (which wins at any realistic desktop/tablet width, later in source
  order than a landscape-orientation rule that would otherwise reduce it further) sets it to exactly
  `var(--mud-appbar-height)` -- clearing the bar's height precisely, with zero px of buffer beyond that
  bare minimum. A page's own heading and primary button sat flush against the bar's bottom edge,
  separated only by its 1px hairline border, in both themes identically.
- **Fixed with a plain `<div class="op-page-content">` wrapping the breadcrumbs and `@Body`, given its
  own `margin-top: 1rem` in `app.css`** -- not a `pt-*` utility class on `MudMainContent` itself: this
  app's own history already found that those compile with `!important` and fully replace the calc()-based
  padding-top rather than add to it (the exact bug behind the "every page's top content rendered hidden
  behind the app bar" fix from 2026-11-15). A plain additive `margin-top` on a wrapper one level in avoids
  that fragility entirely while still landing the same visual result.
- Verified against the real docker-compose stack (not the standalone instance): `getBoundingClientRect()`
  on the app bar and the new wrapper showed an exact 0px gap before the fix and a 15px gap after (1rem,
  matching the CSS value, accounting for sub-pixel layout rounding) -- measured, not eyeballed from a
  screenshot comparison alone. Confirmed in both light and dark mode, and confirmed the historical
  hidden-under-the-app-bar bug has not been reintroduced (content is not clipped; it has strictly more
  clearance than before, never less).

## Fixed — the dark-mode contrast issue found above, now addressed on its own

- Asked to fix it after flagging it as a real-but-separate finding. `PaletteDark` never got the
  deliberate `Background`-vs-`Surface` tint `PaletteLight` already has (`getComputedStyle`, confirmed:
  stock dark `Background`/`Surface` were ~`#32333d`/`#373740`, both lighter than the independently
  chosen `AppbarBackground` `#1e1e1e` -- the bar was darker than the canvas behind it, inverting the
  "surface is lighter than canvas" elevation direction Material, and this theme's own light palette,
  otherwise follow).
- `OpenEmrNextTheme.PaletteDark` now sets `Surface = "#1e1e1e"` (reusing the app bar's existing value,
  not a new color) and `Background = "#121212"` (a classic Material dark canvas, deliberately darker) --
  mirroring `PaletteLight`'s own pattern exactly: the bar matches `Surface` (a card's own tone), the
  canvas is the one deliberately different, darker tone.
- **Found and fixed the same pass, not assumed fine**: `MudDrawer` has its own separate
  `DrawerBackground`, not tied to `Surface` automatically. Checked both themes directly rather than
  assuming symmetry -- light mode's stock default for it already happens to equal white (coincidentally
  matching `AppbarBackground`/`Surface` there), but dark mode's stock default didn't match the `Surface`
  value just set at all, leaving the drawer a visibly different shade from the app bar it sits flush
  against. Added `DrawerBackground = "#1e1e1e"` to match.
- Verified against the real docker-compose stack: `getComputedStyle` on the app bar, drawer, and page
  body now read `rgb(30,30,30)` / `rgb(30,30,30)` / `rgb(18,18,18)` -- bar and drawer identical, canvas
  distinctly darker. Checked a card (Dashboard's "Signed in as" panel) too: it now visibly separates from
  the canvas behind it the same way a light-mode card does, not just the bar/drawer pair. Full solution
  builds clean (0 errors, 0 warnings).

## Changed — accent colors brought up to WCAG AA contrast

- Measured every palette pair against WCAG contrast ratios before changing anything. Body text and the
  purple primary in light mode already passed. Three pairs failed AA's 4.5:1 for normal text:
  - Light `Secondary` teal `#00897b` with white text: 4.32:1 (the Swap button label). Now `#00796b`, 5.32:1.
  - Dark `Primary` purple `#8d6bc2` as text on the dark surface: 3.98:1 (links, "VIEW" actions). Now
    `#a585d8`, 5.51:1, same hue family.
  - Dark filled buttons used white text on those accents: 3.02:1 (primary) and 2.51:1 (secondary).
    Added `PrimaryContrastText`/`SecondaryContrastText` = `#121212` for dark mode only (6.2:1 and 7.45:1).
- Verified on the real stack via computed styles: the dark "Register patient" button renders `#121212`
  text on `rgb(165,133,216)`. Light mode checked visually for regressions.

## Changed — semantic colors (info, warning, error, success) brought up to WCAG AA

- The info alert text measured 3.37:1 on the light canvas (failing), though 5.18:1 on dark. MudBlazor's
  stock semantic tones are too pale for light backgrounds, so each palette now sets its own values:
  - Light: Info `#0868b8`, Warning `#9a5b00`, Error `#c62828`, Success `#2e7d32` (each 4.79:1 or higher on `#f7f7f8`).
  - Dark: Info `#5ab0ff`, Warning `#ffa726`, Error `#ef5350`, Success `#66bb6a` (each 4.78:1 or higher on `#1e1e1e`).
- MudBlazor derives alert text from these base colors rather than using them directly, so the rendered
  text differs slightly from the base. Verified on the live stack: dark info text renders `#389fff`
  (6.01:1 on `#1e1e1e`) and light info text renders `#065493` (well above AA).
- The warning alert was not measured directly on the live page (it only appears signed out); it is
  covered by the palette value's computed contrast, not a rendered check.

## Changed — light mode's canvas and surfaces are no longer pure white

- Reported as pale and glaring, hard to focus on. Light mode had a `#ffffff` app bar and cards on a
  `#f7f7f8` canvas, a very low-contrast wash of white.
- New light tones: canvas `#eceef2` (cool light gray), app bar, cards, and drawer `#fafbfc` (barely
  off-white, so cards still lift off the canvas). Chosen so the accents still clear 4.5:1 on the new
  canvas (info 4.9:1, warning 4.7:1, primary 8.1:1); body text stays near 15:1 on the surface.
- Dark mode is unchanged.

## Changed — light-mode secondary text darkened

- MudBlazor's default light secondary text (`rgba(0,0,0,0.54)`) measured 4.51:1 on cards and 4.39:1 on
  the canvas, failing AA on the canvas. Set `TextSecondary = #5f5f5f` on the light palette: 6.16:1 on
  cards, 5.5:1 on the canvas. Verified on the live page (caption and secondary text compute to
  `rgb(95,95,95)`). Dark mode is unchanged.
