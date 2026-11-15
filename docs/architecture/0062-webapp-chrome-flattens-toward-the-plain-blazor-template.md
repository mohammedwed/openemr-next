# 0062. WebApp's chrome flattens toward the plain Blazor template's look

- **Status:** Accepted
- **Date:** 2026-11-15
- **Builds on:** [0061](0061-webapp-covers-insurance-and-advance-directives.md)

## Context

Two prior rounds this session (user menu/drawer/breadcrumbs, then a fix for the app bar hiding every
page's top content) still left the UI feeling, in the user's words, less "sleek" than a default
`dotnet new blazor` scaffold -- even after confirming the hidden-content bug was fixed and seeing the
result. Asked what specifically felt heavier, the answer was "all of them": the chrome's visual weight
(Material shadows/elevation on every card and the app bar), the nav structure (a persistent left
sidebar), and the color (a solid, saturated purple app bar).

## Decision

1. **Drop the persistent `MudDrawer` sidebar for a flat inline top nav.** With only two enabled routes
   (Dashboard, Patients) plus one disabled placeholder (Encounters), an always-visible side rail added
   chrome weight without adding real navigability. `MainLayout.razor` now renders plain `NavLink`s
   inside the `MudAppBar` itself, styled to look like the default template's own nav: muted text,
   purple only on the active route's text and underline. The breadcrumbs move below the app bar, into
   `MudMainContent`.
2. **Neutralize the app bar's color.** MudBlazor's own default paints the whole app bar with `Primary`
   unless told otherwise; `OpenEmrNextTheme` now sets `AppbarBackground`/`AppbarText` explicitly (white
   / dark-text in light mode, a dark surface / light-text in dark mode) so purple shows up only as an
   accent -- the active nav link, primary buttons -- not as a solid block covering the entire top bar.
3. **Reduce elevation/shadow use across the app.** App bar and drawer-replacement nav: `Elevation="0"`
   with a hairline `border-bottom` instead of a Material shadow. Cards/papers/tables that were
   `Elevation="2"`: now `1`, with alerts switched from filled to `Variant="Outlined"` + `Dense`. The
   patient detail page's two feature sections (insurance, advance directives) moved from one shadowed
   `MudExpansionPanels` to two separate `MudPaper Elevation="1"` panels, each a flat `Elevation="0"`
   expansion panel inside -- visually lighter, and clearer about where one feature group ends and the
   next begins.

**Two real bugs found while making this change, neither caught by the build (0 warnings throughout)**:

1. **Blazor's CSS-isolation scope attribute is never added to `NavLink`'s own rendered `<a>`.** A first
   attempt styled the new nav with a `MainLayout.razor.css` scoped stylesheet (`.op-nav-link { ... }`,
   compiled to `.op-nav-link[b-xxxxxxxx]`); every rule silently failed to match. Confirmed by inspecting
   the live DOM: the rendered anchor's `class` was exactly `op-nav-link active` with no `b-xxxxxxxx`
   attribute at all, unlike the plain `<span>` written directly in the same file (which did get scoped).
   Fixed by moving the nav styles into the existing global `wwwroot/css/app.css` instead -- the same
   fix needed for any attempt to style a framework-provided component's root element from a parent's
   scoped stylesheet.
2. **`Color="Color.Secondary"` on `MudText` was used to mean "muted gray text," but it actually maps to
   the theme's `Secondary` accent (our teal, `#00897b`), not the muted `TextSecondary` palette token.**
   This made several supporting lines (the patient detail page's DOB/Pid line, "signed in as" labels,
   empty-state captions) render in loud teal instead of quiet gray -- directly undermining the "reduce
   visual weight" goal it was meant to serve. Fixed by switching every such usage to
   `Class="mud-text-secondary"` (MudBlazor's utility class for `--mud-palette-text-secondary`), confirmed
   against the installed package's own compiled CSS (`.mud-text-secondary{color:var(--mud-palette-text-secondary)}`
   vs `.mud-secondary-text{color:var(--mud-palette-secondary) !important}` for the two different,
   similarly-named classes). The one legitimate use of `Color="Color.Secondary"` -- the insurance
   "Swap" button, deliberately distinct from the primary Add/Save actions -- was left unchanged.

**A follow-up round, after the user still found the result "not elegant" with no further specifics
given**: rather than ask a fourth round of categorization questions, the page was inspected directly
(computed styles in a running browser) for concrete, fixable defects instead of guessed at again. Two
were found:

4. **`PaletteLight.Background` (the page canvas) and `Surface` (`MudPaper`/`MudTable`'s own
   background) were both pure white** -- confirmed via `getComputedStyle`: `--mud-palette-background`
   and `--mud-palette-surface` were both `rgba(255,255,255,1)`, and a card's only separation from the
   page was a barely-visible 1px/12%-opacity shadow. It read as unstyled, not flat-by-design. Fixed by
   giving `Background` a faint neutral tint (`#f7f7f8`), leaving `Surface` white -- the standard
   light-canvas/white-card pattern, still with no heavy shadow or saturated color.
5. **"Dashboard" (or "Patients") appeared three times stacked within ~100px**: once as the active top
   nav link, once as a single-item breadcrumb directly below it, and once again as the page's own
   `<h1>`. A breadcrumb with no real trail adds nothing over the nav + heading already there. Fixed by
   only rendering `MudBreadcrumbs` when there are 2+ items (an actual hierarchy, e.g. "Patients /
   Register" or "Patients / Patient") -- top-level pages no longer show one at all.
6. **`MudDatePicker` fields were missing `Variant="Variant.Outlined"`**, rendering with MudBlazor's
   default underlined style while every `MudTextField`/`MudSelect` on the same form used the outlined
   variant -- a visible inconsistency within a single form. Fixed by adding the same `Variant` to every
   `MudDatePicker` across `Register.razor` and `Detail.razor`.

## Consequences

- The chrome now visually resembles the plain Blazor template's own aesthetic the user compared it
  against: flat, light, mostly-neutral, with brand color reserved for emphasis rather than used as a
  background fill.
- Losing the persistent drawer means the nav no longer scales past a handful of routes without redesign
  (no overflow/grouping behavior exists yet). Acceptable at the app's current size (two real routes);
  revisit if the route list grows materially.
- Global CSS (`app.css`) is now the deliberate home for anything that needs to style a framework
  component's own rendered root (confirmed here specifically for `NavLink`), not scoped
  `.razor.css` files, which only reach elements written directly in that same `.razor` file.

## Alternatives considered

- **Keep the persistent drawer but make it collapsible-by-default.** Rejected: still chrome the user
  named as feeling dated regardless of its default open/closed state; removing it entirely was the more
  direct response to "nav structure... feels dated."
- **Style `NavLink` via a `::deep` combinator instead of moving the CSS to the global stylesheet.**
  Rejected after checking: `::deep` lifts the scope requirement for selectors *nested after* it, but
  `.op-nav-link` itself -- the element being targeted directly, not a descendant of some other scoped
  element -- would still need the scope attribute it never receives. It does not apply to this case.

## Related decisions

Builds on [0061](0061-webapp-covers-insurance-and-advance-directives.md)'s WebApp pages; restyles the
chrome introduced across the session's two prior MainLayout rounds without changing any of their
underlying behavior (user menu, breadcrumbs, sign-in gating).
