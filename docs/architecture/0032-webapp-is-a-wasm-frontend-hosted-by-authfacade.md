# 0032. The frontend is a Blazor WebAssembly app, hosted by AuthFacade itself

- **Status:** Superseded by [0033](0033-gateway-fronts-webapp-and-authfacade.md) — the render-mode decision (WebAssembly over Blazor Server) stands; the hosting decision (bundled into AuthFacade) does not
- **Date:** 2026-10-31
- **Builds on:** [0024](0024-browser-authorization-code-flow-with-pkce.md), [0026](0026-openiddict-client-drives-the-authorization-code-flow.md), [0027](0027-persist-tokens-in-the-session-cookie.md), [0029](0029-yarp-proxies-browser-sessions-to-openemrs-api.md), [0031](0031-nswag-client-generated-from-a-committed-snapshot.md)

## Context

The project's first real frontend, beyond the static Login UI page, needed two decisions before any code:
which Blazor render mode, and where its static files are actually served from. Both turned out to be the
same decision in disguise.

**Render mode.** Blazor Server and Blazor WebAssembly were weighed:

- **Blazor Server** renders on a server, with the browser only ever talking to that server's own host over
  a SignalR circuit. That host is not "the browser" in the sense ADR 0024/2026/2027 already built for: it
  cannot forward the `AuthFacadeSession` cookie to AuthFacade on the user's behalf (that is a server-to-server
  HTTP call, which carries none of the browser's cookies), so it would need to fall back to the Bearer-token
  password/refresh grant (ADR 0013) and manage tokens itself server-side — quietly stepping around the entire
  browser-session design this project just finished building. It also introduces a second, unrelated
  per-instance state problem (a SignalR circuit pinned to one server), on top of the one ADR 0028 already
  found with AuthFacade's own session.
- **Blazor WebAssembly** runs in the actual browser. It genuinely is "the browser" in the existing design: it
  can complete `/oidc/login`, hold only the `AuthFacadeSession` cookie, and call `/bff/openemr/**` or
  `/api/v1/me` the same way any other browser client would. Its host is just static files — no per-connection
  server state to scale.

WebAssembly was chosen specifically because it is the only option that reuses ADR 0024–0029 rather than
quietly bypassing them.

**Where the static files are served from.** That choice has a consequence: for the browser to actually
attach the `AuthFacadeSession` cookie to the app's own `fetch` calls, the app must be same-origin with
AuthFacade. `SameSite=Lax` (ADR 0026) does not send a cookie on a cross-site `fetch`/XHR request — only on a
top-level navigation, which is why the separate-origin Login UI (ADR 0026) has always worked: it only ever
does full-page redirects, never a cross-origin `fetch` carrying the cookie. An interactive dashboard needs
real `fetch` calls to load data without reloading the page, which the Login UI never needed.

## Decision

1. **`OpenEmrNext.WebApp`**, a standalone Blazor WebAssembly project (`src/Services/WebApp`), styled with
   MudBlazor (full interactivity this time, unlike the Login UI's static-only use of it). Its `HttpClient`'s
   `BaseAddress` is the app's own origin — deliberately not a configured AuthFacade URL, since same-origin is
   the whole point.
2. **AuthFacade.Api hosts it directly** — the classic "ASP.NET Core hosted Blazor WebAssembly" shape, just
   applied to an existing API host instead of a purpose-built one. `AuthFacade.Api` takes a project reference
   to `OpenEmrNext.WebApp` (for its build output, not its code) and a new `Microsoft.AspNetCore.Components.
   WebAssembly.Server` package reference. `WebAppHostingExtensions.MapWebApp` maps the WASM runtime and other
   static assets via `MapStaticAssets()` (the current replacement for `UseStaticFiles()` that resolves the
   fingerprinted file names `index.html` references); `MapWebAppFallback` serves `index.html` for any
   unmatched path outside AuthFacade's own route prefixes, so the WebAssembly app's client-side router can
   take over for a deep link or a refresh.
   - This makes AuthFacade a BFF in the fullest textbook sense: it does the OIDC login, holds the session,
     proxies API calls (ADR 0029), and now serves the frontend too, all from one origin — not scope creep,
     the standard shape of this pattern (the same shape Duende.BFF's own reference architecture uses, which
     came up when YARP was chosen for the proxy in the first place).
3. **The dashboard shell is intentionally thin**: a landing page that calls `GET /api/v1/me` through the
   generated `AuthFacadeClient` (ADR 0031) and shows the signed-in identity, or a "Log in with OpenEMR" link
   when unauthenticated, plus disabled stub nav entries for Patients and Encounters. Nothing beyond identity
   is real yet — Patient Management and Clinical Care (ROADMAP M4/M5) have not started, so there is nothing
   else to show honestly.
4. **One accepted narrowing to AuthFacade's existing error behavior.** Once any fallback endpoint exists,
   ASP.NET Core's router treats "a real endpoint matched this path but rejected the request's content-type"
   the same as "nothing matched at all," and defers to the fallback. Concretely: a non-form `POST
   /api/v1/connect/token` used to get the framework's own 415 (ADR 0019/0022); it now gets
   `MapWebAppFallback`'s 404 instead, since `/api/v1/...` is one of the prefixes that fallback explicitly
   reserves. A route-level fix (excluding those prefixes via a regex constraint on the fallback's own route
   pattern, so it was never a routing candidate for them at all) was tried and rejected: the regex constraint
   also broke the fallback's ability to match the literal root path, which is the common case the whole
   feature exists for. Both 404 and 415 are 4xx rejections of a malformed request, so the narrower, code-level
   prefix check was accepted instead, with the one changed test updated to match and explain why.

## Consequences

- A new project and a new dependency AuthFacade.Api did not have before (serving a frontend's static files).
  This is a deliberate widening of AuthFacade's responsibilities, consistent with the BFF pattern it already
  implements, not an accidental one.
- `TestApi` needed `MapStaticAssets`'s manifest path passed explicitly: the default, entry-assembly-derived
  manifest name resolves to the test host's own assembly in-process, not AuthFacade.Api's.
- The in-process test suite (`WebAppHostingTests`) can only verify routing decisions (status codes), not
  actual static-asset byte content: `WebRootFileProvider` cannot find a referenced project's static content
  once `TestApi` overrides the host's content root for its in-process test, the same way `MapStaticAssets`'s
  default manifest resolution cannot — real content was confirmed separately by actually running AuthFacade
  and fetching it with curl (fingerprinted names resolved, MudBlazor's CSS loaded, the WebAssembly boot files
  were reachable).
- The one accepted narrowing above (point 4): a non-form request to `/api/v1/connect/token` now answers 404
  instead of 415. `ApiEndpointTests`'s matching test was renamed and its assertion updated, with the reasoning
  inline rather than treating it as a regression to chase back to 415.
- The Login UI's own default post-login destination (`BrowserFlow:DefaultReturnUrl`) still points at itself,
  not at this new app. Wiring the two together — deciding whether the Login UI still has a role once a real
  dashboard exists, or whether `/oidc/login`'s default return destination should become this app's own
  origin — is a separate, not-yet-made decision, left open deliberately rather than folded into this one.

## Alternatives considered

- **Blazor Server.** Rejected; see Context.
- **A separately-hosted WebAssembly app**, on its own origin. Rejected: it cannot attach the
  `AuthFacadeSession` cookie to its own `fetch` calls without either CORS configuration or loosening
  `SameSite`, neither of which was judged worth it against the zero-cost alternative of hosting it from
  AuthFacade directly.
- **Excluding AuthFacade's reserved path prefixes at the route level** (a regex constraint on the fallback's
  pattern), to avoid narrowing the 415 case at all. Rejected; see Decision 4 — it broke the fallback's own
  common case (the literal root path) in the process of trying to protect an edge case.

## Related decisions

Builds on the browser-session design (ADR 0024, 0026, 0027) this app is the first real consumer of, the BFF
proxy (ADR 0029) it will eventually call for anything beyond identity, and the generated client (ADR 0031) it
uses instead of hand-rolled `HttpClient` calls. Does not resolve ROADMAP M3 (the gateway) or M4/M5 (the
services this app's stub nav points at) — those remain not started.
