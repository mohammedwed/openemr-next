# 0033. A gateway, not AuthFacade, gives WebApp and AuthFacade one shared origin

- **Status:** Accepted — supersedes [0032](0032-webapp-is-a-wasm-frontend-hosted-by-authfacade.md)
- **Date:** 2026-10-31
- **Builds on:** [0024](0024-browser-authorization-code-flow-with-pkce.md), [0029](0029-yarp-proxies-browser-sessions-to-openemrs-api.md), [0030](0030-bff-proxy-forwards-to-the-gateway-not-each-service.md), [0032](0032-webapp-is-a-wasm-frontend-hosted-by-authfacade.md)

## Context

ADR 0032 had AuthFacade itself host `OpenEmrNext.WebApp`'s static files, reasoning that the dashboard's
`fetch` calls needed same-origin with AuthFacade for the `AuthFacadeSession` cookie to attach. That reasoning
was correct, but the conclusion conflated two different things: *same origin* and *same container*. A
reverse proxy in front of two separate containers presents one external origin just as well as bundling them
into one — and this project already has a name for that proxy: the gateway ADR 0030 described as "a single
hop for anything beyond OpenEMR," just not yet built.

Keeping WebApp inside AuthFacade's own container was also in direct tension with ADR 0030's own reasoning.
That ADR rejected letting the BFF proxy grow into a general-purpose router "by accretion" — accepting a new
kind of responsibility each time something needed AuthFacade's origin. Hosting the frontend's static files
was exactly that kind of accretion, just dressed differently. ADR 0032's "Alternatives considered" section
only weighed loosening `SameSite`/CORS against bundling, and missed the actually-correct third option: keep
WebApp as its own deployable, and solve same-origin with a proxy in front of both.

One more subtlety surfaced building this: same-origin has to hold for the *entire* OIDC round trip, not just
the dashboard's later `fetch` calls. `OpenEmr:RedirectUri` is a single, pre-registered value independent of
how a login started — so if it still pointed at AuthFacade's own address, OpenEMR would redirect the browser
back there directly, bypassing the gateway entirely for that one request. The `AuthFacadeSession` cookie that
`/callback` sets would then be read by the browser as having come from AuthFacade's address, not the
gateway's — exactly the mismatch this whole change exists to avoid, just moved to a different step of the
flow. `OpenEmr:RedirectUri` (and the seed's matching `redirect_uri` registration) had to move to the
gateway's own address for the fix to actually hold end to end.

## Decision

1. **`OpenEmrNext.WebApp` is its own container again**, built and served by nginx — a standalone static
   Blazor WebAssembly app needs no .NET runtime at serving time, just a static file server with an
   `index.html` fallback for client-side routing (`src/Services/WebApp/nginx.conf`). Its own Dockerfile's
   build context is the repository root, not its own directory: it still references
   `OpenEmrNext.AuthFacade.Client` (ADR 0031), a sibling service directory a context scoped to
   `src/Services/WebApp` could not reach — the same cross-context constraint AuthFacade's own Dockerfile
   never had to deal with, since every project *it* references already lives inside its own directory.
2. **AuthFacade reverts to exactly its ADR 0029/0030 shape** — token broker, BFF, proxy to OpenEMR. The
   `Microsoft.AspNetCore.Components.WebAssembly.Server` dependency, the project reference to WebApp, and
   `WebAppHostingExtensions` are all removed. The one test this undoes (`Non_form_request_returns_415`,
   briefly renamed to expect 404 under ADR 0032) is back to asserting the original 415 — that narrowing
   existed only because of the fallback endpoint ADR 0032 added, which no longer exists.
3. **A new `OpenEmrNext.Gateway` project** (YARP, the same library ADR 0029's proxy already uses) is the one
   external origin. Its routing needs no exclusion pattern or regex constraint — unlike the fragile attempt
   inside AuthFacade's own fallback that ADR 0032 settled for: YARP's routes are ordinary ASP.NET Core route
   templates, and normal route specificity (`/api/{**catch-all}` beats `/{**catch-all}` automatically) sends
   AuthFacade's own prefixes (`/api`, `/oidc`, `/callback`, `/bff`, `/health`, `/openapi`, `/scalar`) to
   AuthFacade and everything else to WebApp, regardless of registration order.
4. **`OpenEmr:RedirectUri` (and the seed's `AUTHFACADE_REDIRECT_URI`) point at the gateway**
   (`http://localhost:${GATEWAY_HTTP_PORT:-7130}/callback`), not AuthFacade's own address — see Context.
   This changes the *registered* redirect URI the whole OAuth client shares; it affects the Login UI's own
   login flow too (ADR 0026), but not its outcome: `/callback` is simply reached through the gateway instead
   of AuthFacade directly, for every caller, and the final redirect to whatever `returnUrl` resolved to is
   unaffected either way.
5. **WebApp's own container publishes no host port.** It only works correctly reached through the gateway —
   publishing it directly would let someone load it from the wrong origin and silently find the session
   cookie never attaches, which is worse than not offering that path at all.

## Consequences

- Three containers now exist where ADR 0032 had one extra responsibility folded into AuthFacade: `webapp`,
  `gateway`, and AuthFacade back to its narrow shape. This is the real microservice separation the project's
  own stated direction calls for, not a bundle held together by convenience.
- Verified against the real stack (`docker compose build && docker compose up`), not just unit tests: the
  gateway correctly serves the dashboard at `/`, proxies `/health` to AuthFacade, and `/oidc/login` redirects
  to OpenEMR's `/authorize` with `redirect_uri=http://localhost:7130/callback` — the gateway's address. The
  seed's registered `oauth_clients.redirect_uri` was confirmed to match exactly via a direct database query.
  Driving the flow further with `curl` and a persistent cookie jar reached OpenEMR's real login form (through
  the full gateway → AuthFacade → OpenEMR chain) and confirmed OpenIddict's own correlation cookie is set
  correctly along the way.
- **Not verified: a real browser completing the full login and the dashboard reading `/api/v1/me`
  afterward.** No browser automation was available in this environment, and completing the login via `curl`
  needs real OpenEMR admin credentials, which are deliberately not something this session reads from `.env`
  — one attempt with the snapshot's documented demo credential did not succeed, and repeated guessing against
  a real login endpoint was deliberately not pursued. This remains open until confirmed manually.
- The integration test suite's `AUTHFACADE_REDIRECT_URI` override (`StackConfig.cs`) still defaults to
  AuthFacade's own address, not the gateway's — now genuinely mismatched with what the seed registers. The
  one test that uses it only asserts the absence of `invalid_client`, not a specific redirect_uri error, so
  this has not needed fixing yet; documented as a known gap in `docs/configuration.md` rather than fixed
  speculatively.
- `docker-compose.yml`, `.env.example`, and `docs/configuration.md` all gained the gateway's port and
  settings; `docs/architecture/README.md`'s ADR 0032 entry is marked superseded by this one.

## Alternatives considered

- **Keep AuthFacade hosting WebApp** (ADR 0032, as originally decided). Superseded; see Context.
- **CORS plus `fetch` credentials, instead of a shared origin.** Would have let WebApp stay on its own
  origin without a gateway. Rejected: on `localhost` specifically, cookies ignore port when deciding
  same-site (only the scheme and registrable domain count), which can make a cross-port setup look like it
  works in local dev purely by that accident — `fetch`'s default of not sending credentials cross-origin,
  and the need for explicit CORS headers to let the page read the response, would still need solving, and
  neither generalizes cleanly to a real deployment on genuinely different domains. The gateway's single
  origin needs no CORS configuration at all, in development or in a real deployment alike.

## Related decisions

Supersedes [0032](0032-webapp-is-a-wasm-frontend-hosted-by-authfacade.md). Builds on
[0030](0030-bff-proxy-forwards-to-the-gateway-not-each-service.md)'s reasoning for why AuthFacade should not
grow new responsibilities by accretion, and is itself an instance of M3's gateway from ROADMAP.md, built
earlier than scheduled specifically to let WebApp exist as its own deployable now rather than waiting.
