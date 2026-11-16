# Changelog 2026-11-16

## Added — dashboard with sample content

- **Dashboard** (`Pages/Home.razor`) populated for layout review: four KPI cards, a new-patients line chart,
  a today's-appointments donut with a count legend, an upcoming-appointments table, recent activity,
  a visits-by-weekday bar chart, and pending-work progress bars. All figures are sample data, labeled as such
  on the page; no patient data is read.
- The page is widened to `MaxWidth.Large`. Sign-in is unchanged.
- **Fixed during review:** the legend rows threw `IndexOutOfRangeException` (a `for` loop captured by Razor's
  child lambdas). Switched to `foreach` over the zipped labels and counts.
- **Charts:** MudBlazor 9 API (`ChartSeries<double>`, `ChartLabels`), with the donut on `ChartSeries`.
- Verified in Chrome at 1568px desktop width against the local stack: no console errors after the fix.
  Phone width was not verified (the window resize did not change the viewport).

## Fixed — patient view on phone width

- **Section actions wrap** onto their own line when the card is narrow (`op-section-header` and
  `op-section-actions` in `app.css`), so "Swap levels" no longer splits across two lines.
- **Tabs show scroll arrows always** (`AlwaysShowScrollButtons`), so a second tab cannot be hidden off the edge.
  The arrows also show on desktop, faintly. Accepted as a small trade-off.
- Verified in a 390px frame (no sideways scroll; actions on one line below the title; arrows visible) and on desktop.

## Verified — patient frontend polish round

- **Phone width:** checked by loading the page in a 390px frame (the window resize tool does not change the viewport
  here). No sideways scroll; insurance rows stack as label/value pairs. Two problems found, not fixed yet:
  the section action buttons wrap ("Swap levels" on two lines), and the second tab ("Advance directives")
  is cut off at the right edge, so it is not obvious there is another tab.
- **Light mode:** checked on the patient page. Cards and text read clearly.
- **Keyboard focus (fixed):** MudBlazor removed the focus outline on buttons, and the filled primary button showed
  no visible focus at all. `app.css` now draws a 2px outline for buttons, tabs, and links on `:focus-visible`.
  Verified after a hard reload: the outline is visible on "Swap levels".
- **503 wording:** verified. With the backend stopped and six submits from the add-policy dialog, the breaker opened
  and the dialog showed "The patient service is temporarily unavailable. Nothing was saved. Try again in a moment."
  No test policy was saved; after the backend was restarted the insurance table still showed only the original row.
- **Breadcrumb:** the "Loading…" state is transient; after the load it shows the patient's name.
- **Not yet verified:** the placeholder rows (too brief to capture) and the upload "Reading file…" state (needs a file upload).
- **Observed gateway behavior:** with the backend down, submits fail with the generic "Could not add this policy"
  message, not the network-error message, because the gateway answers first.

## Changed — patient frontend polish

- **Register form:** field-specific required messages (replacing "Required"); typed dates allowed in the
  date pickers; a prompt before leaving with unsaved input ("Discard this registration?"), which "Keep
  editing" cancels. Verified in the browser: the prompt appears, and Keep editing keeps the typed value.
- **Dialogs:** typed dates allowed; the add-policy fields have specific required messages.
- **Upload:** "Reading file…" with a progress bar while the file is read, and the button stays disabled until
  it is ready. A read failure shows a message instead of an unhandled error.
- **Loading:** sections show placeholder rows instead of a spinner, so content does not jump when it arrives.
- **Outage wording:** a 503 (the circuit breaker is open, or the service is down) says the service is
  temporarily unavailable and nothing was saved.
- **Headings:** the patient list title is an `h1`.
- **Breadcrumb:** shows "Loading…" while a patient loads, and falls back to "Patient" if the patient does not load.
- **Not done, on purpose:** Enter-to-submit and first-field focus in the dialogs. Enter could submit from a
  dropdown or date field by accident, and focus behavior needs a browser check before it is changed.
- **Not verified in the browser:** phone width (the window resize did not take effect), light mode, keyboard focus,
  the placeholder rows, the upload progress state, and the 503 wording (the breaker was not tripped).

## Changed — register form width and layout

- Container widened from `Small` (600px) to `Large` (1280px), the same width as the patient list and detail pages.
- First and last name share a row, and sex and date of birth share a row, on screens 600px and wider. They
  stack on phones. Email fields stay full width.
- Built and redeployed. **Not yet checked visually in the browser** (the browser tool's permission check
  was unavailable at the time).

## Changed — register patient page

- **Sex is a dropdown** (Male, Female, Other, Unknown) instead of free text. The values match the ones the
  codebase already uses for administrative sex. The domain accepts any text, so existing free-text records are unaffected.
- **Date of birth is required and cannot be in the future.** Before, an empty date did nothing when Register was
  clicked, with no message. The calendar's maximum date is today.
- **Network failures are handled**: the sign-in check shows "Try again" instead of an unhandled error, and a
  failed submit says nothing was saved.
- **After registering**, the page opens the new patient, and the message shows their patient ID.
- **Heading is `h1`**, and the spinner has a label.
- Verified in the browser: validation on empty submit (no patient was created). Not verified: a successful
  registration (it would create a patient in the dev database), the future-date rule (typing into the picker did
  not register), and the network-failure messages.
- Open: `PlaceholderRegisteredById` is the same tracked gap as the directive dialogs (pending decisions, item 1).
- Observation for the architect: sex values are capitalized in the registration path and the tests, but the FHIR
  mapper matches lowercase `"female"`. Worth checking which casing the stored data uses.

## Fixed — search results out of order; age read once per load

- **Search results could arrive out of order.** A slow response for an earlier term could overwrite the results
  for a newer one. A search now applies its result only if the term on screen is still the one it searched for.
  A stale failure is ignored the same way. Not tested by forcing a slow response in the browser; checked that
  search and the empty-result message still work.
- **Age is read once per patient load** (`_today` in `Detail.razor`), not on every render.
- **Still open, tracked in `docs/patient-view-pending-decisions.md`:** uploads read the file into memory as base64
  before sending. That's part of the upload-size item, which needs an API change.

## Changed — no-API items from the patient view backlog

- **Home page sign-in** now uses the shared `TryGetCurrentUserAsync`, the same rule as the patient pages.
  Behavior is unchanged: an API rejection shows signed out, and a network failure still throws.
- **Patient list search** waits 350 ms after the last keystroke before searching, so typing sends one request
  instead of one per letter (verified in the browser: one request for "Brow"). Enter and the search icon
  still search at once. A network failure during search now shows the search error instead of escaping.
- **Items that need an API change or a decision** are listed in `docs/patient-view-pending-decisions.md`
  for the architect.

## Changed — shared patient section card

- `Pages/Patients/PatientSectionCard.razor` holds the card, title, summary, actions, and the three states
  (loading, failed first load with "Try again", failed refresh with the old data kept and a warning). The
  insurance and advance-directive panels now supply only their title, actions, and content.
- Actions are hidden until there is data to act on. Before, the insurance buttons showed during loading.
- Verified in the browser: both tabs render the same as before the refactor.
- Open item 5 in the review pass is done.

## Changed — patient view review pass (robustness, structure, accessibility)

- **Count bug fixed.** A failed count request no longer turns a loaded patient into "Could not load this patient".
  Sections now load after the patient is shown, and their failures stay inside their own section.
- **Sections load once, in the parent.** `Detail.razor` fetches insurance, directive status, and documents, and
  passes them to the panels. Before, the parent and the panels fetched the same lists. Tab counts come from the
  same data, so they cannot disagree with the table. Each section shows its own loading state, error, and "Try again".
- **Panels are presentational.** `PatientInsurancePanel` and `PatientAdvanceDirectivesPanel` render what they
  are given and raise `Changed` after a successful change. The parent reloads.
- **Network failures are handled** in the four change dialogs ("Could not reach the patient service") and in
  the sections, instead of surfacing as unhandled errors.
- **Upload limit lowered to 20 MB**, checked before the file is read, with a message that gives the size.
  The old 30 MB limit could let a file through the browser that the API then rejected.
- **Shared dialog style** in `Shared/DialogDefaults.cs`.
- **Accessibility:** the patient's name is the page `h1`. "Swap levels" explains why it is disabled when there are no policies.
- Verified in the browser: heading, both tabs with counts, no stray spinners, outage shows the patient error,
  and "Try again" recovers. Section-level errors and the upload limit were not browser-tested.

## Open items — patient view and insurance

Tracked here so they are not lost. Ordered by how much they matter.

1. **Hardcoded user ID.** `PlaceholderUserId = 1` in the status and upload dialogs means every directive change is
   attributed to user 1. Needs a decision on mapping the signed-in subject to OpenEMR's numeric user ID.
2. **Automated tests.** The webapp has no test project. Needs approval before running `dotnet test`.
3. **API request size.** The API uses ASP.NET Core's default request limit (about 30 MB, not confirmed in this
   environment). Documents travel as base64 in JSON, which adds about a third. Raising the upload limit needs the
   API limit raised, or the upload contract changed to multipart or streaming.
4. **Browser checks not done:** tab counts after a successful change, a successful dialog submit, the upload size
   rejection, network-failure messages in the dialogs, and a single section failing while the patient loads.
   Submitting changes would modify the dev database, so these need approval.
5. **Duplicated panel shell.** Both panels repeat the same card, loading, and error markup. Could become one component.
6. **Stored insurance level changed** from "Secondary" to "Primary" between two checks. Resolved: the owner changed it.

## Changed — patient view sections are cards

- Insurance is one outlined card (heading, actions, table). Advance directives has two cards: status, and
  documents. Before, the sections sat flat on the same background, so they read as one stacked column.
- Verified in the browser on both tabs.

## Changed — breathing room in the patient view tabs

- Tab panels use more padding (`pa-6`). Insurance and document tables are no longer dense, so rows have
  normal height. The gap under each section heading is larger, and the divider between status and documents has more space.
- Verified in the browser: layout only.
- Open: the insurance row displayed "Secondary" in an earlier check and "Primary" in the latest one. No swap
  was submitted (only the dialog was opened and cancelled). The cause has not been confirmed.

## Changed — patient view polish, round 2

- **Breadcrumb names the patient** ("Patients / BrowserTest Patient"). The layout builds breadcrumbs from the
  URL alone, so a scoped `Shared/PatientPageContext` lets the patient page publish the name.
- **Tab counts.** "Insurance (1)" and "Advance directives (1)". The parent loads both counts and refreshes them
  after a successful change (the panels raise `Changed`). Counts are unknown (no suffix) while loading or
  if the call fails. Trade-off: the counts add two requests on load, and the panels still load their own
  data, so the same lists are fetched twice.
- **Banner** has more padding and a larger name heading.
- **Section headings** are `h6` on both tabs, so they read as headings instead of small labels.
- **Empty states** give the next action: "Add the first policy" and "Upload the first document".
- Verified in the browser: breadcrumb, tab counts, banner. Not verified: the empty-state buttons, and a change
  that updates the counts (that would submit a change to the dev database).

## Changed — patient view reorganized

- Basic information (patient ID, date of birth with age, email) is a banner above the tabs, always visible.
  The name is shown once, in the banner. The Overview tab is removed, since it only repeated the banner.
- Tabs hold only the two areas opened on demand: Insurance (default) and Advance directives.
- Advance directives: status and its action sit in one block, separated by a divider from the documents
  and their upload action.
- Verified in the browser: banner, both tabs, and the advance-directives layout. Webapp only; no API changes.

## Changed — patient view polish (webapp only, no API change)

- Browser tab title shows the patient's name instead of "Patient".
- Date of birth on the Overview shows the age in completed years: "1990-01-01 (36 years)".
- Document sizes read "11 B", "1.5 KB", or "2.0 MB" instead of raw byte counts (`Shared/Formatting.cs`).
- Verified in the browser: title, age, and size. Not changed: the API, the client, and package references.

## Changed — patient view opens on an Overview with basic information

- New first tab, **Overview**, is the default. It shows name, patient ID, date of birth, and email as a
  description list. Insurance and Advance directives are the second and third tabs and load when opened.
- The header keeps only the name, so the patient ID is no longer shown twice.
- Blank email values now display as "—" on the overview and on the patient list. Before, an empty string
  (not null) left the cell empty.
- Verified in the browser: Overview is the default tab with all four values, and Insurance loads on click.
- The overview has only the fields the patient summary API returns. Phone, address, and sex would need the
  API to return them.

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

## Added — Patient Management DDD design set (draft)

- New `docs/patient-management/` (README plus eight documents): bounded context, ubiquitous language,
  context map, domain model (aggregates and 51 numbered rules), workflows and events, data ownership and
  legacy mapping, gaps and evolution, and 22 Mermaid UML diagrams. Target-design perspective, with every
  capability and rule marked built, partial, target, or deferred. Status is **Draft**: nothing in it is
  accepted, and its 22 decisions (doc 07, section 6) are unanswered. No code changed.
- Findings recorded, not fixed (each is tracked with an id in doc 07):
  - The patient API is reachable without signing in: the Gateway attaches the shared secret to every
    request on the patient routes (doc 03, M1). The one critical finding.
  - Registration runs duplicate scoring after its transaction commits, so a scoring failure returns an
    error for a patient that was created, and a retry registers them twice (doc 05, F-1).
  - Patient numbers are random up to 19 digits, which also breaks legacy's `MAX(pid)+1` allocator, and
    legacy's REST API (`POST /api/patient`) is still enabled in this stack as a second insert path
    (doc 06, G-2 and G-3).
  - Nothing audits changes or reads, only `PatientRegistered` is ever written to the outbox, and nothing
    delivers it (doc 06, G-8; doc 05, F-5 and F-6).
- Not verified (doc 07, section 7): the Mermaid diagrams were not rendered, and that legacy screens
  open rows this service created, which tables legacy populates for contacts, and whether legacy writes
  its audit `log` on patient creation were read from code and schema only.
- Corrected along the way: related persons. Legacy's `DemographicsRelatedPersonsService` uses a
  `patient_related_persons` table that no SQL file in the legacy checkout creates; the schema models a
  related person as `contact` → `person` → `contact_relation`.

## Added — Microservice Migration Playbook (docs only)

- New `docs/microservice-playbook/` (README plus 14 documents): a framework-neutral guide to extracting a
  domain service from a legacy system with DDD and hexagonal architecture, derived from building Patient
  Management. Covers principles and the nine early decisions (01), discovery and strategic design (02),
  layering and its enforcement (03), tactical modeling (04), legacy-data integration (05), APIs, gateway,
  and events (06), security (07), quality and testing (08), delivery and operations (09), a milestone
  roadmap with a per-slice cycle (10), checklists (11), lessons learned (12), templates (13), and a map to
  this repository (14). 204 numbered rules, each with its rationale; every checklist item cites a rule id.
  Documents 01-13 name no product or framework; only 14 does.
- Lessons are recorded as they happened, including the open ones (the ungated patient API, the unread
  outbox, client-supplied acting users). Doc 14's deviation register lists 28 places this repository does
  not yet follow the playbook.
- **Three new findings, not yet in the findings register** (`docs/patient-management/07-gaps-and-evolution.md`
  §3), found while checking the repository against the playbook:
  - CI regenerates and diffs only the AuthFacade OpenAPI snapshot, not PatientManagement's.
  - `scripts/README.md` omits `seeds/patient-management-db.sh` and `dev/generate-patientmanagement-openapi.sh`.
  - `docs/configuration.md` and `docs/environments.md` have no PatientManagement or `INTERNAL_GATEWAY_SECRET`
    entries (`.env.example` does).
- README's "Documentation" section links the playbook beside the authentication guides.
- **Verified:** every rule id cited in the set is defined exactly once (scripted check); no product names
  outside doc 14 (grep). **Not verified:** the Mermaid diagrams (including one `quadrantChart`) were not
  rendered, and the claims in doc 14 about repository status were read from documents and code, not run.

## Added — M5 (Clinical Care) design and decisions

- **ADR 0064:** Clinical Care shares OpenEMR's database and coexists with the legacy encounter writers;
  slice order is Create Encounter, then Record Vitals, then relay and `PatientRegistered` consumption;
  events travel by an HTTP push behind a port; an interim `patient_data` lookup is the named stand-in,
  with its removal criterion.
- **`docs/clinical-care/01-create-encounter-slice.md`:** research notes with file and line evidence, the
  `CC-1` to `CC-11` rule catalogue (parity vs new), ownership matrix, slice plan, findings and not-verified
  lists. Written from the playbook (`docs/microservice-playbook/`), with PatientManagement as the reference.
- **Findings recorded, not fixed:** `form_encounter` has at least six legacy writers; M4's patient
  registration writes no `uuid_registry` row (legacy does), which needs a check (F-CC-3).
- ROADMAP M5 moves to In progress; its "own database" bullet is marked superseded. No code yet.

## Added — Clinical Care scaffold (M5, slice 1 step: skeleton)

- **Six source projects** under `src/Services/ClinicalCare/`: `Domain`, `Application.Ports`, `Application`,
  `Infrastructure`, `Composition`, `Api`, with the same reference rules and package versions as
  PatientManagement (playbook ARC §2). `Domain` exposes internals only to `Infrastructure` and `UnitTests`
  (DOM-5, DOM-9).
- **Three test projects** under `tests/Services/ClinicalCare/`: `UnitTests`, `ArchitectureTests`,
  `IntegrationTests` (Testcontainers MariaDB, ready for the integration slice). All nine are in the solution.
- **`LayerDependencyTests`:** the inward-only layering rules, run against assemblies loaded by name. A
  `Every_layer_assembly_loads` test guards against a layer being renamed or dropped. While a layer has no
  code the rules hold trivially; they gain force as code lands.
- **Composition:** `ClinicalCareDbOptions` and `InternalGatewayOptions`, both `[Required]` and validated on
  start (OPS-3). **Api:** shared-secret handler with constant-time compare (SEC-3), a `/api/v1` group that
  requires authorization (SEC-1), problem-details error handling, request validation, OpenAPI and Scalar in
  Development.
- **Deliberately not built yet:** `Domain.ReadModels` (waits for the first real read model, ADR 0054),
  `Client` (needs an OpenAPI snapshot), the DbContext, the Dockerfile, the compose service, the seed script
  and the gateway route. No endpoints exist.
- Verified: `dotnet build` of the whole solution, 0 errors. Tests were not run.

## Added — Clinical Care slice 1: Create Encounter, end to end

- **Domain:** `Encounter` aggregate, `EncounterCreation` (collects every error, CC-2 to CC-14), `PatientReference`,
  `EncounterIdentity`, and `EncounterClassRule` (pure, cited). Identity and the clock are inputs, not
  generated inside the aggregate (DOM-7, DOM-8).
- **Ports and use case:** `CreateEncounterUseCase` resolves the patient (404 if unknown), reads the class list
  and facility through ports, validates, and only then takes a number from the shared counter, so a rejected
  request never burns one.
- **Adapters (legacy anti-corruption layer):** one transaction writes `form_encounter`, the legacy `forms` row,
  `uuid_registry` and the outbox row. Retry-safe by uuid. The encounter number comes from the shared
  `sequences` counter in its own short transaction, so the counter's row lock is not held across the insert.
  Events are wrapped in a pinned camelCase envelope (`eventId`, `schemaVersion`, `correlationId`, ...).
- **API:** `POST /api/v1/encounters` (201 + Location, 400 listing every error, 404 problem+json). Request
  attributes carry the same messages as the domain rules (ARC-11). Flat route because `/api/v1/patients/**`
  already belongs to PatientManagement at the gateway.
- **Deploy:** `scripts/seeds/clinical-care-db.sh` (idempotent; one commented grant each; column-level grants on
  `users`, `groups`, `facility`, `list_options` and `patient_data`), `Dockerfile`, compose services
  `seed-clinical-care-db` and `clinical-care` (no published port), gateway cluster and route with **its own
  secret**, and the two new variables in `.env.example`, `new-env.sh` and CI. **An existing `.env` needs
  `CLINICAL_CARE_DB_PASSWORD` and `CLINICAL_CARE_GATEWAY_SECRET` added by hand** (`new-env.sh` never overwrites).
- **Tests written (not run):** domain and use-case unit tests with rule ids in the names; invariant and layer
  architecture tests; integration tests against a MariaDB container built from the **real** `snapshots/db.sql`
  DDL, with the **real seed script** creating the production principal the tests connect as, covering the
  adapters, grants (what the principal may not do), concurrency of the counter, the HTTP pipeline (401s, 201,
  400s, 404), the exception handler and startup validation; and one gateway route test for the stack.
- **Findings recorded** (`docs/clinical-care/01-create-encounter-slice.md` section 7): provider existence is
  unchecked (F-CC-6); PatientManagement's seed grants `SELECT` on all of `users`, including a `password` column
  (F-CC-7); create is not idempotent (F-CC-8); PatientManagement holds the `sequences` lock across its insert
  (F-CC-9).
- **Verified:** `dotnet build` of the whole solution, 0 errors; the Docker image builds; `docker compose config`
  validates. **Not verified:** no test has been run, and the stack has not been started.

## Added — Clinical Care read side and typed client

- **Reads:** `GET /api/v1/encounters/{uuid}` and `GET /api/v1/encounters?patientUuid=&limit=&offset=` (newest first
  **by date**, bounded to 100 per page, default 25, `total` returned; no "all encounters" read exists). A
  soft-deleted encounter is 404, by `NOT EXISTS` on the `newpatient` forms row, as legacy does (CC-9).
- **Tolerant read model** (`Domain.ReadModels`, ADR 0054): a half-filled legacy row (no date, class or number, or a
  pid that matches no patient) is returned, not turned into an error; a row with no uuid is left out of lists.
- **`OpenEmrNext.ClinicalCare.Client`:** NSwag client generated from the committed `OpenAPIs/ClinicalCare.json`,
  itself produced by running the real API (`scripts/dev/generate-clinicalcare-openapi.sh`). A new test fails if
  the snapshot drifts from the live document; PatientManagement has no such guard.
- **Tests written (not run):** adapter tests against deliberately awkward legacy rows (deleted, half-filled, no uuid,
  late-entered old visit, several `newpatient` rows with one deleted); paging; the GET endpoints incl. 401, 404,
  400; `PageRequest` and read use-case unit tests; OpenAPI document and drift tests; architecture rules extended
  for `ReadModels`.
- **Findings added:** F-CC-10 (`forms.authorized` is a `tinyint` that legacy fills with the provider id) and F-CC-11
  (the OpenAPI document marks nullable request properties `required`, as PatientManagement's does).

## Verified — Clinical Care slice 1 against tests and the running stack

- **Tests run:** unit 63/63, architecture 23/23, integration 77/77, gateway stack 8/8. The first integration run was
  74/76: one test bug (`{}` in a format string) and **one real defect**.
- **Fixed — `forms.authorized` overflow (F-CC-10):** legacy writes the provider id into a `tinyint`; legacy runs
  with `sql_mode = ''` and silently truncates above 127, but this service is strict, so provider id 999 failed.
  `authorized` is now `1` (the 0/1 flag every other legacy caller passes). A recorded deviation (CC-7).
- **End to end:** the new services were started on the live stack; an encounter was created through the gateway
  (201, number 3 from the shared counter) and read back. **Legacy's own `EncounterService` reads the row and its
  patient-screen list shows it** under "Office Visit". The browser half of LEG-15 is pending an OpenEMR login.
- **Version skew found:** the stack runs OpenEMR **8.0.0**; the source tree used for research is **8.6.0-dev**,
  and every file I cited differs. Key claims were re-verified in the running container: they hold, except that
  8.0.0's `EncounterService` has no soft-delete handling (only its UI does), so CC-9 is stricter than 8.0.0's
  REST service. Line citations in the slice doc are to 8.6.0-dev.
- **`.env`:** `CLINICAL_CARE_DB_PASSWORD` and `CLINICAL_CARE_GATEWAY_SECRET` were generated and added (the file is
  git-ignored; a copy of the previous one was kept in the session scratchpad).

## Added — Clinical Care slice 2: Record Vitals

- **ADR 0065:** vitals are **stored in US units, and the API takes and returns explicit units**. Found by reading the
  8.0.0 code the stack runs: the database always holds lb, in and degF, a global only changes *display*, and the
  wire carries a bare number, so `70` would be silently misread. A weight is `{ "value": 70, "unit": "kg" }`; an
  unknown or wrong-dimension unit is a 400.
- **Domain:** `Vitals`, `VitalsRecording` (collects every error; plausibility bounds, all marked *new*),
  `UnitConversion` (legacy's `MeasurementUtils` factors, half-away-from-zero rounding like PHP), `BmiRule`
  (legacy's formula and strictly-greater-than thresholds). BMI is derived, never accepted.
- **Adapters:** one transaction writes `form_vitals`, the `forms` link (vitals have no encounter column), `uuid_registry`
  and the outbox, retry-safe. Reads use `EXISTS` against `forms`, treat a stored `0` as "not recorded", and read
  free-text blood pressure as absent instead of failing.
- **API:** `POST`, list and get under `/api/v1/encounters/{uuid}/vitals`, `?units=metric`. The existing gateway route
  already covers it: no new route, no new secret. `VitalsRecorded` names the measurements taken but carries no values.
- **Deploy:** one new grant, `form_vitals` `SELECT, INSERT`.
- **Verified:** unit 155/155, integration 114/114 (first run, no failures). On the live stack, legacy's own
  `VitalsService` and `EncounterService::getVitals` read the row; legacy's metric display shows the same numbers as
  `?units=metric` to the digit.
- **Open:** F-CV-1 (`form_vitals_calculation`), and the legacy-UI view pending a login.

## Added — Clinical Care slice 3: the outbox relay and `PatientRegistered` consumption (ADR 0066)

- **Relay (PatientManagement):** a hosted service drains `patient_management_outbox` and pushes each event to Clinical Care over HTTP, behind
  an `IEventPublisher` port. In-order, leased (a crashed relay loses nothing), exponential back-off, dead-letter after 8 attempts or a permanent
  rejection (logged at error level for an alert), safe with several instances. Off unless `EventRelay:Enabled`; when on, missing config fails startup.
- **Envelope built at publish time** from the row, so the one **pre-existing, never-delivered PascalCase row** and new rows leave identically
  with pinned camelCase names, `eventId` (a stable v5 id derived from the row), `schemaVersion` 1 and `correlationId`. 401, 403 and 404 from the
  consumer back off and recover rather than dead-letter; only 400 and 422 are permanent.
- **Consumer (Clinical Care):** `POST /internal/events`, hidden from the public OpenAPI and the client, no gateway route, its **own secret and
  scheme** (`CLINICAL_CARE_EVENTS_SECRET`): the gateway cannot deliver events and the relay cannot call the public API. Idempotent on `eventId`:
  the id and the effect commit in one transaction. Unknown types are `202`, unsupported versions `400`, a tolerant reader.
- **The `patient_data` stand-in is gone.** Clinical Care keeps a reference (uuid, pid) in `clinical_care_patient_ref`; its encounter reader joins
  that, and its `patient_data` grant is **revoked**. Patients registered before events existed are copied once by the seed script, as root.
- **Seed scripts now `REVOKE ALL` before granting**, so a removed or narrowed grant really disappears from a database that ran an older version.
- **PatientManagement fixes in the same change:** **F-CC-7** `users` grant narrowed from the whole table (it has a `password` column) to
  `(id, uuid)`; **F-CC-3** it now records the uuids it creates (`patient_data`, `insurance_data`, `documents`) in legacy's `uuid_registry`,
  in the same transaction; **F-CC-9** a document's number is drawn from the shared counter in its own short transaction instead of holding the
  counter's row lock across the insert.
- **Verified:** Clinical Care unit 160, architecture 31, integration 139; PatientManagement unit 166, integration 73 (all pre-existing tests
  still pass); all green on first run. **Live:** the old row was delivered; a new patient's encounter is `404` at once and `201` about 5 s later;
  with the consumer stopped the event stays in the outbox backing off and is delivered within ~9 s of it returning (and the patient registered
  during the outage then gets an encounter); both grant changes confirmed on the live database; FHIR still `200` on the narrowed grant.
- **New secret:** `CLINICAL_CARE_EVENTS_SECRET` (in `.env.example`, `new-env.sh`, CI, compose). Added to your local `.env`.
- **Findings:** F-PM-1 to F-PM-7 in `docs/clinical-care/03-patient-registered-consumption.md` (the 5 s consistency window, `drive_uuid` registry
  entries, no purge of processed events, event id derivation, no dead-letter alert yet).

## Verified — shadow comparison of encounter creation (ROADMAP M5)

- Created three synthetic scenarios through **legacy's own `insertEncounter`** and through the new API on the live stack, and compared every column
  of `form_encounter`, `forms` and `uuid_registry`: **142 identical, 2 differ**, both being rule CC-4 (REST-path midnight date vs a real time).
  Details and limits in `docs/clinical-care/01-create-encounter-slice.md` section 12.

