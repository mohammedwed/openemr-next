# Changelog 2026-10-31

## Summary

The hand-rolled browser flow ADR 0024 described (`BrowserFlowEndpoints` and its supporting classes) was
dead code — superseded by ADR 0026's OpenIddict.Client flow, but left in place and unreferenced. It is
removed, along with everything that was only reachable from it. AuthFacade can now also act as the BFF for
a browser session: a YARP reverse proxy attaches the real OpenEMR access token the session cookie carries
(ADR 0027) and forwards to OpenEMR's own API, so a browser that only ever holds the session cookie can still
reach it (ADR 0029). Two new ADRs (0028, 0030) record known limitations and a routing decision for later,
without changing any already-accepted outcome.

## Removed

- **The ADR 0024 hand-rolled browser flow and everything only it used**, now that ADR 0026 has fully
  superseded it: `BrowserFlowEndpoints` (and its ten tests, previously left `Skip`ped rather than deleted),
  `OidcFlowCookieProtector`/`OidcFlowState`, `SessionCookieProtector`, `Pkce`, `BrowserFlowErrorCodes`. None
  of these were mapped into routing or referenced anywhere else; confirmed by reference-counting every
  public/internal type across `src/` and `tests/`, and by a full analyzer build
  (`EnableNETAnalyzers`/`EnforceCodeStyleInBuild`/`AnalysisLevel=latest-all`) across every AuthFacade project,
  which surfaced only ordinary code-quality suggestions, nothing unused beyond this branch.
- The now-dangling `AddSingleton<OidcFlowCookieProtector>()`/`AddSingleton<SessionCookieProtector>()`
  registrations in `AuthFacadeApiExtensions`, and a stale comment in `AuthFacadeEndpoints` referring to the
  deleted class.

## Added

- **A BFF proxy from a browser session to OpenEMR's own API.** See ADR 0029.
  - `Yarp.ReverseProxy`, added to the Api project.
  - `OpenEmrBffProxyServiceCollectionExtensions.AddOpenEmrBffProxy` builds one route/cluster pair in memory
    from `OpenEmr:BaseUrl` (not `appsettings.json`-driven config, since the destination is only known from
    `IConfiguration` at startup) and registers a request transform that reads the session's access token
    (`HttpContext.GetTokenAsync(SessionScheme, "access_token")`) and attaches it as `Authorization: Bearer`.
    A session with no token attaches no header; OpenEMR then rejects the request itself, since the proxy
    makes no authorization decision of its own (ADR 0013's passthrough, extended to every proxied call).
  - `OpenEmrBffProxyEndpoints.MapOpenEmrBffProxyEndpoints` maps `/bff/openemr/{**catch-all}`, requiring the
    `AuthFacadeSession` scheme specifically — not the Bearer-or-session either/or `/me` uses, since a caller
    that already holds a bearer token has no need for this proxy.
  - `OpenIddictTokenCapture` (`Api/OpenIddictClient/`): `CaptureTokens` was pulled out of
    `OpenIddictClientEndpoints` into its own file, since it is a self-contained concern (translating
    OpenIddict's internal token names to ASP.NET Core's plain convention) unrelated to endpoint routing.
- **ADR 0028**, recording that AuthFacade's browser login and session cookie assume a single running
  instance: OpenIddict's per-process SQLite state-token store, its per-process dev-only
  encryption/signing certificates, and the session cookie's own Data-Protection-encrypted ticket (no shared
  key ring) all independently break across more than one replica. No code change; it documents an existing
  constraint of ADR 0026/0027 before any multi-replica work is attempted.
- **ADR 0030**, recording that the BFF proxy will forward to a future API gateway (ROADMAP M3) as a single
  hop for any backend service beyond OpenEMR, rather than growing a route per service itself. The proxy's
  existing OpenEMR cluster (ADR 0029) is unaffected. No code change; there is no gateway and no second
  service yet.
- `FakeOpenEmrServer` gained two routes for the proxy tests: `/apis/default/api/ping` (echoes back the
  `Authorization` header it received) and `/apis/default/api/missing-patient` (always answers 404).
  `TestApi` gained a test-only `/test/sign-in-without-tokens` endpoint, to simulate a session with no access
  token — a case the real login flow can never produce, since OpenEMR always issues one.

## Changed

- `ROADMAP.md`: M1's "persist data-protection keys" item now cross-references ADR 0028 (it closes one of
  three gaps, not all of them); M3 now records the BFF-proxy-to-gateway decision ahead of time (ADR 0030).
- `README.md`: the status line, the AuthFacade feature list, the "browser login is not implemented"
  authentication-boundary bullet, and the known-gaps list were all out of date relative to ADRs 0023–0030 —
  updated to describe the browser flow, the session cookie, and the BFF proxy as built, with their
  documented gaps (no `api:oemr` scope yet, no silent refresh, single-instance only). Also added
  `src/Services/LoginUi/` to the repository structure tree, missing since it was introduced.

## Tests

- Unit tests: 236 pass, up from 231. New: 5 for the BFF proxy (token attached for a real session, no-cookie
  request rejected, bearer-only request rejected, no-token session forwards with no `Authorization` header,
  a non-200 OpenEMR response relays back unchanged). Removed: the 10 `Skip`ped `BrowserFlowEndpointTests`,
  deleted with the code they tested.
- Architecture tests: 12 pass, unaffected.
