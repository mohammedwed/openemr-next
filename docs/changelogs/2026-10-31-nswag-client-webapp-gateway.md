# Changelog 2026-10-31 (continued): typed client, WASM dashboard, gateway

## Summary

Before building a real frontend, AuthFacade's API needed a typed client instead of hand-rolled `HttpClient`
calls — NSwag generates one from a committed OpenAPI snapshot (ADR 0031), which also surfaced and fixed a
real bug in `TokenRequest`. A Blazor WebAssembly dashboard (`OpenEmrNext.WebApp`) is the first real consumer
of it. The first attempt had AuthFacade itself host the dashboard's files (ADR 0032); corrected on review to
a separate container fronted by a new gateway (ADR 0033), since bundling was the same kind of accretion ADR
0030 already rejected for the BFF proxy.

## Added

- **`OpenEmrNext.AuthFacade.Client`**, an NSwag-generated typed client built from a committed snapshot
  (`OpenAPIs/AuthFacade.json`), not build-time generation — AuthFacade's startup requires
  `OpenEmr:ClientId`/`ClientSecret`, deliberately never committed, so making generation unconditional on
  every build would have broken a bare `dotnet build` for everyone. `scripts/dev/generate-authfacade-openapi.sh`
  regenerates the snapshot by briefly running the real app with placeholder credentials. See ADR 0031.
- **`TokenRequest` now normalizes an empty optional field to `null`** before validation runs. The generated
  client always serializes all four form properties, so a password-grant call sent `refresh_token=""` and
  got `400 invalid_request`, even though that field does not apply to the grant in use. Confirmed safe:
  `TokenEndpoints.cs`'s dispatch never passes the irrelevant field to a domain value object either way.
- **`OpenEmrNext.WebApp`**, a Blazor WebAssembly dashboard: calls `GET /api/v1/me` through the generated
  client and shows the signed-in identity, or a login link if unauthenticated, plus disabled stub nav for
  Patients and Encounters (nothing beyond identity is real yet — ROADMAP M4/M5 have not started).
- **`OpenEmrNext.Gateway`** (YARP): the one external origin a browser talks to. Routes AuthFacade's own
  path prefixes to it and everything else to WebApp, via plain ASP.NET Core route specificity — no
  exclusion pattern or regex constraint needed, unlike the fragile attempt the first (reverted) version of
  this work tried inside AuthFacade's own fallback. See ADR 0033.

## Changed

- `OpenEmr:RedirectUri` (and the seed's `AUTHFACADE_REDIRECT_URI`) now point at the gateway's address, not
  AuthFacade's own: the *entire* OIDC round trip, not just the dashboard's later API calls, has to land on
  one consistent origin for the session cookie to work — OpenEMR's redirect is a single, pre-registered
  value, independent of how a login started.
- `docker-compose.yml`, `.env.example`, `docs/configuration.md`: gained the gateway's port (`7130`) and
  settings. WebApp's own container publishes no host port — it only works correctly reached through the
  gateway.

## Reverted along the way

- AuthFacade briefly hosted WebApp's static files directly (ADR 0032, superseded same day by ADR 0033).
  That code was fully reverted, not left behind: the `Microsoft.AspNetCore.Components.WebAssembly.Server`
  dependency, the project reference to WebApp, and `WebAppHostingExtensions` are all gone. The one test this
  had changed (`Non_form_request_returns_415`, briefly expecting 404 because of the fallback endpoint ADR
  0032 added) is back to asserting the original 415.

## Tests

- Unit tests: 240 pass (up from 236, then briefly 245 during the reverted ADR 0032 attempt, back to 240 once
  that reverted). New: `AuthFacadeClientTests` proves the generated client completes a login and reads
  identity through the real pipeline — the test that caught the `TokenRequest` gap in the first place.
- Architecture tests: 12 pass, unaffected.
- Verified against the real stack (`docker compose build && up`), not just unit tests: the gateway serves
  the dashboard at `/`, proxies `/health` to AuthFacade, and `/oidc/login` redirects to OpenEMR with
  `redirect_uri=http://localhost:7130/callback` — confirmed against the seed's actual database row via a
  direct query, not just AuthFacade's own config.
