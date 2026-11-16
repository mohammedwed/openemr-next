# 0063. WebApp restores the nav drawer

- **Status:** Accepted
- **Date:** 2026-11-15
- **Supersedes:** [0062](0062-webapp-chrome-flattens-toward-the-plain-blazor-template.md)'s nav-structure decision
  only (its color and elevation decisions are unaffected -- see Consequences)

## Context

ADR 0062 dropped the persistent `MudDrawer` sidebar for a flat inline top nav, reasoning that with only
two enabled routes, an always-visible side rail added chrome weight without adding real navigability. It
named one explicit trigger to revisit that: "the route list grows materially."

That trigger hasn't fired -- the route count is unchanged (Dashboard, Patients, one disabled placeholder).
The user nonetheless asked directly to restore the drawer. Before doing the work, this was flagged back
to them as reopening 0062 rather than carried out silently, including the tradeoff (a sidebar scales
better; it also reintroduces more visual weight than the flat nav) and the recommendation to wait for
0062's own named trigger. The user's response was to proceed anyway. This ADR records that explicit
choice and the shape of the restoration, rather than treating it as a quiet reversal.

Separately, in the time since 0062 shipped, the flat top nav had grown a second, parallel responsive
nav surface: a hamburger dropdown (`MudMenu`) for viewport widths 0062 never considered, built to close a
real mobile-rendering gap in the flat nav. Restoring the drawer removes the need for that second surface
entirely -- `MudDrawer`'s own `Responsive` variant already covers both desktop and mobile with one
component, which is "the needed changes" this restoration asked for alongside bringing the drawer back.

## Decision

1. **`MudDrawer Variant="DrawerVariant.Responsive"` replaces both the flat inline nav
   (`op-nav--desktop`) and the hamburger dropdown (`op-nav--mobile`) it had grown.** One `MudNavMenu`
   feeds `MudNavLink`s from the same `_navItems` source already in place, at `Breakpoint="Breakpoint.Md"`
   (the library's own default, not the 600px breakpoint the removed hamburger dropdown used -- a
   persistent side rail competing with page content for width makes sense down to a narrower point in
   the viewport range than a top nav row does, so it keeps its own, wider breakpoint rather than
   inheriting the dropdown's).
2. **A drawer-toggle `MudIconButton` replaces the hamburger `MudMenu`** at the app bar's start (`Edge.Start`),
   matching the position the original pre-0062 drawer toggle used. It keeps the hamburger<->close icon
   morph the hamburger dropdown had added (`_drawerOpen` driving the icon), now reflecting the drawer's
   own open state instead of a dropdown's.
3. **The disabled "Encounters" placeholder keeps its "Soon" badge and real accessible name**, both added
   after 0062 shipped, inside `MudNavLink`'s own `Disabled` state -- which already handles the
   focusable/announced-as-disabled concerns the flat nav's hand-rolled `tabindex`/`role`/`aria-disabled`
   trio existed to work around in the first place. That hand-rolled trio is gone with the flat nav itself;
   a first-party disabled nav-link component already covers it.
4. **Not reverted**: the account-menu avatar, its `ActivatorContent` click wiring, the `UserState` enum,
   and the flat/neutral app-bar color and elevation choices from 0062's other two decisions. Only 0062's
   nav-*structure* call is superseded here; its color and elevation reasoning is independent of whether
   the nav lives in a top row or a side rail, and nothing about it was in question.

## Consequences

- The nav scales to more routes again without a redesign -- the concern 0062 accepted as a known
  limitation of the flat nav no longer applies, since that's gone.
- Desktop pages lose some content width to the open drawer that the flat nav didn't take, the direct
  tradeoff 0062's own "alternatives considered" section named when it rejected keeping a
  collapsible-by-default drawer.
- `op-nav--desktop`/`op-nav--mobile`, their CSS, `_mobileNavOpen`, and `IsActiveRoute` (`MudNavLink`
  computes its own active state, the same way `NavLink` already did) are all dead code once the drawer
  replaces them, and are removed in the same change rather than left behind unused.
- 0062's color/elevation decisions (flat, neutral app bar; `Elevation="0"`/hairline borders over
  Material shadows) are unaffected and applied to the restored drawer too (`Elevation="0"` with the same
  hairline-border treatment the app bar already uses), not reverted to the heavier `Elevation="1"` the
  pre-0062 drawer had -- those were never in question here, only the nav's structure.

## Related decisions

Supersedes [0062](0062-webapp-chrome-flattens-toward-the-plain-blazor-template.md)'s nav-structure
decision specifically. Restores the drawer [0061](0061-webapp-covers-insurance-and-advance-directives.md)-era
`MainLayout.razor` first introduced, adapted to carry everything added to the nav since 0062 (the "Soon"
badge, real accessible naming for the disabled placeholder, the account-menu avatar) rather than a plain
revert to that earlier version.
