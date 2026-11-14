# Changelog 2026-11-03

## Summary

Two small WebApp/LoginUi fixes (missing MudBlazor fonts, a broken post-login landing page) and a CI
fix for the OpenAPI snapshot generator, plus a circuit breaker on the one remaining unguarded
AuthFacade call. Separately, four reusable migration guides were written up from this project's own
evidence, generalized for another team to use, plus a comprehensive handbook that inlines everything
the guides cite out to ADRs.

## Added

- **A circuit breaker on WebApp's calls to AuthFacade.** AuthFacade.Api's own breakers (ADR 0041) only
  guard its calls to OpenEMR — WebApp → AuthFacade was the one hop with none.
  `AuthFacadeCircuitBreakingHandler` sits on the `HttpClient` passed into the generated
  `AuthFacadeClient`, using the same failure semantics (4xx isn't a failure; 5xx/408/timeouts/
  connection errors are), so it survives `AuthFacadeClient` being regenerated and needs no changes to
  generated code.
- **Four reusable legacy-auth migration guides**, each generalized so another team can use it, written
  out of this project's own evidence:
  - `legacy-auth-migration-framework.md` — the framework, each claim cited back to the ADR it came
    from.
  - `legacy-auth-facade-framework.md` — the same framework restated generically, no ADR citations or
    OpenEMR/.NET-specific names, for a team in a different stack.
  - `auth-facade-implementation-guide.md` — a sequential ten-phase build order, each step with a
    verifiable "done when" and the mistakes specific to it.
  - `fast-legacy-stack-startup.md` — the snapshot-and-restore technique behind
    `scripts/snapshot/create.sh`/`scripts/stack/up.sh`, cutting the legacy stack's CI boot from roughly
    twenty minutes to under one. Also published as a public gist; this file is canonical.
  - All four linked from README's documentation index, which nothing referenced them from before.
- **A comprehensive legacy-auth migration handbook**, one self-contained document carrying everything
  the four guides above cite out to ADRs, explained in place instead: orientation and a real
  case-study narrative, the decisions (stakeholder/team questions, eight legacy-system facts, decision
  graphs, tradeoff table, accepted-risk ledger), the same ten-phase build order, a reference section
  (hardening, config, testing, security testing, observability, CI/CD, containers, multi-service
  concerns), the phase-gated migration checklist plus a separate production-readiness bar, and one
  stack's concrete library/command appendix. Linked from README as the comprehensive entry.

## Fixed

- **MudBlazor's required Google Fonts (Roboto, Material Icons) weren't loaded** in either WebApp or
  LoginUi, despite `MudBlazor.min.css`/`.js` already being wired up — icons fell back to literal text
  and typography used the browser's generic sans-serif instead of what MudBlazor's spacing is tuned
  for.
- **The post-login landing page was broken.** `/logout` always lands on a `returnUrl`-less page
  (`LogoutEndpoints.cs`), and that page then defaulted a successful re-login back to itself — a bare
  page with no dashboard chrome, even though WebApp was right there sharing the same origin through
  the gateway (ADR 0032). `AUTHFACADE_POST_LOGOUT_REDIRECT_URI`/`BrowserFlow__DefaultReturnUrl` now
  point at WebApp's own origin instead of LoginUi's, and `Login.razor`'s own `returnUrl`-less default
  follows the same origin for anyone who lands there directly. WebApp's `Home.razor` gained the
  matching `?error=` handling LoginUi's page already had, so a failed login landing there now shows a
  clear message instead of silently falling back to the login button.
- **CI's OpenAPI snapshot generator failed on every run.** `generate-authfacade-openapi.sh`'s throwaway
  AuthFacade instance uses `appsettings.Development.json`'s `OpenEmr:BaseUrl`
  (`https://localhost:9300`), but `OpenEmrReadinessGate`'s plain `HttpClient` has no cert bypass, so
  every poll failed on OpenEMR's untrusted self-signed dev cert — burning the gate's own 60s non-fatal
  timeout before it gave up and bound Kestrel anyway, longer than the script's own 30s health-check
  patience, which killed the instance first. The real Compose stack never hits this (container-to-
  container, plain HTTP); pointing this script at OpenEMR's plain-HTTP host port instead sidesteps the
  cert entirely. Verified against the live local stack: the script now completes immediately and
  regenerates a byte-identical snapshot.

## Tests

- No automated test project exists yet for WebApp or LoginUi — the font and landing-page fixes were
  verified manually against the live stack, as their commit messages describe. The circuit breaker and
  CI fix were likewise verified against the live stack directly, not via a new or existing test suite.
