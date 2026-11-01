# 0041. Circuit breakers guard the remaining calls to OpenEMR

- **Status:** Accepted
- **Date:** 2026-11-01
- **Resurrects:** [0017](0017-circuit-breakers-guard-openemr-calls.md)

## Context

ADR 0017 wrapped every outbound call to OpenEMR in a circuit breaker; ADR 0035 deleted all of it — the
decorators, the options, the Polly v7/v8 compatibility shim their one `Polly.Core` usage needed — along with
the password grant they guarded ("the Polly *pattern* is sound and reusable — the decorators were just
pointed at the one call graph that is gone... reapplying it to the BFF proxy's or OpenIddict's own outbound
calls, which currently have none, is a deliberately separate follow-up, not bundled into this removal").

Since then, two server-to-server calls to OpenEMR exist again, neither guarded:

1. **The token exchange during `/callback`** (OpenIddict.Client's own call to `/oauth2/default/token`,
   plus `OpenEmrConfigurationManager`'s JWKS fetch behind it) — ADR 0026.
2. **The BFF proxy's forwarded API calls** (`/bff/openemr/...` → OpenEMR's REST/FHIR API) — ADR 0029.

Without a breaker, "OpenEMR is down" does not fail fast — it fails *slow*: each request still waits out its
own `HttpClient` timeout individually before giving up, tying up AuthFacade's own threads and connections
for the whole window, and (for the token/JWKS calls) hammering an already-struggling OpenEMR with more
traffic instead of backing off.

Neither call site is reachable the way ADR 0017's `IIdentityProvider`/`IExternalTokenVerifier` decorators
were — that clean interface boundary is gone along with the layers it lived in (ADR 0035). The actual HTTP
calls now happen inside third-party libraries: OpenIddict.Client.SystemNetHttp for the token exchange, YARP
for the BFF proxy. Each needed its own extension point, found by reading their source rather than assuming
either worked the way ADR 0017's own decorators did:

- **OpenIddict.Client.SystemNetHttp exposes `OpenIddictClientSystemNetHttpOptions.HttpResiliencePipeline`** —
  a `Polly.ResiliencePipeline<HttpResponseMessage>` slot, set via
  `options.UseSystemNetHttp().SetHttpResiliencePipeline(...)`, wrapping every HTTP call it makes.
- **YARP exposes `IForwarderHttpClientFactory.WrapHandler`**, the documented way to insert a
  `DelegatingHandler` into a cluster's outbound handler chain; a custom factory registered with a plain
  `AddSingleton` after `AddReverseProxy()`'s own `TryAddSingleton` for the same service type is what
  actually gets resolved (last registration wins).
- **The JWKS fetch is AuthFacade's own `HttpClient.GetStringAsync` call** (`OpenEmrConfigurationManager`),
  already fully within AuthFacade's own code — no library extension point needed, just a pipeline parameter.

## Decision

1. **`CircuitBreakerOptions` and `CircuitBreakerPipeline`, resurrected nearly verbatim from ADR 0017**, now
   under `Api/Resilience/` (the clean `Infrastructure` layer they lived in is gone — ADR 0035 — so this is
   as close to that layer as the current, two-project shape gets). One set of thresholds
   (`OpenEmr:CircuitBreaker`), shared by three independent breaker *instances* — one per call site, since
   each guards a different OpenEMR endpoint with its own failure pattern, even though all three ultimately
   answer the same question ("is OpenEMR up").
2. **The token exchange's breaker is handed to OpenIddict.Client.SystemNetHttp directly**
   (`SetHttpResiliencePipeline`); the JWKS fetch's is threaded through `OpenEmrConfigurationManager`'s
   constructor and wraps its `GetStringAsync` call, its `BrokenCircuitException` added to the same
   catch clause that already swallows a failed fetch and keeps the cached keys.
3. **The BFF proxy's breaker is handed to YARP via `CircuitBreakingForwarderHttpClientFactory`**, a small
   `ForwarderHttpClientFactory` subclass whose `WrapHandler` inserts a `DelegatingHandler` that runs the
   pipeline and, on `BrokenCircuitException`, returns a synthetic `503` directly — the proxy's own
   passthrough principle (ADR 0013) relays that unchanged, so the browser sees a plain `503`, not an
   unhandled exception bubbling out of the forwarder.
4. **None of the three count a 4xx as a failure** — the same reasoning ADR 0017 already established: a
   rejected grant (a bad or replayed code) or a resource the caller cannot reach (a 404) says nothing about
   whether OpenEMR itself is up. Only a connection problem, a timeout, a `408`, or a `5xx` opens the circuit.
5. **`/callback` gets an explicit `try`/`catch` around `AuthenticateAsync`**, new in this ADR (ADR 0017 never
   needed one — its decorators sat behind a clean interface that OpenIddict.Client's own internals do not
   give AuthFacade for the token exchange). Confirmed necessary by testing against the real stack: a
   `BrokenCircuitException` thrown mid-exchange propagates out of `AuthenticateAsync` as a raw exception,
   not a failed `AuthenticateResult` the way a rejected grant does — without this catch, it would reach the
   browser as an unhandled 500 instead of the generic `login_failed` redirect ADR 0026 already relies on.
   The catch has no `AuthenticationProperties` to recover a `returnUrl` from (the exchange failed before
   OpenIddict could produce one), so it falls back to `BrowserFlowOptions.DefaultReturnUrl` — the same
   fallback the success path's own `result.Properties?.RedirectUri ?? "/"` is tightened to use here too,
   for the same reason.
6. **The Polly v7/v8 compile-asset exclusion shim returns**, verbatim from before ADR 0035 removed it, in
   both the Api project's and the unit test project's `.csproj` — the same `Polly.CircuitBreaker.BrokenCircuitException`
   name collision between OpenIddict.Client.SystemNetHttp's transitive Polly v7 dependency and the
   `Polly.Core` v8 these breakers use directly, now that something uses `Polly.Core` v8 again.

## Consequences

- OpenEMR being down now fails fast for both call sites after a handful of failures, instead of every
  request individually waiting out its own timeout.
- Verified against the live Compose stack, not only in-process tests: stopping the `openemr` container and
  repeatedly hitting `/callback` through the real gateway produced a real
  `Polly.CircuitBreaker.BrokenCircuitException: The circuit is now open and is not allowing calls.` in
  AuthFacade's own logs once enough real connection failures had accumulated, with every attempt — before
  and after the circuit opened — still answering the same graceful `?error=login_failed` redirect, never a
  raw 500. Restarting `openemr` and waiting out `BreakDuration` produced a real HTTP round trip again (logged:
  `Received HTTP response headers after 105.5592ms - 400`, OpenEMR's own genuine rejection of the still-bogus
  test code) — full automatic recovery, not just an open-forever circuit.
- The BFF proxy's breaker and the "a 4xx never counts as a failure" guarantee for both call sites are
  covered by in-process unit tests (`CircuitBreakerTests`) against a fake OpenEMR server with low,
  deterministic thresholds — not re-verified against the live stack, since reaching the BFF proxy at all
  needs a signed-in session, which needs real OpenEMR credentials unavailable in this session.
- `OpenEmrCircuitBreakers`'s failure predicates are duplicated in shape (one for `HttpResponseMessage`, one
  for `string`) rather than unified, since the JWKS fetch's `GetStringAsync` has nothing to inspect a status
  code on — it already throws `HttpRequestException` for any non-success response.

## Alternatives considered

- **Give the BFF proxy's breaker its own dedicated exception type**, mirroring ADR 0017's
  `IdentityProviderUnavailableException`/`TokenVerifierUnavailableException`. Rejected: those existed so a
  clean interface's caller could distinguish "OpenEMR is unavailable" from any other failure. The BFF proxy
  has no such caller-side interface anymore — `CircuitBreakingForwarderHttpClientFactory` is the only thing
  that ever sees a `BrokenCircuitException` here, and it already does the one thing needed with it (answer
  `503`) without a caller needing to catch a specific type.
- **Thread `IOptions<CircuitBreakerOptions>` through DI instead of reading configuration directly in
  `AddOpenIddictClient`/`AddOpenEmrBffProxy`.** Rejected: both call sites build their pipelines
  synchronously, during service registration, before the host or its DI container exist to resolve
  anything from — the same constraint `BuildRegistration`'s own direct read of `OpenEmrOptions` already
  works under (ADR 0026 point 4's reasoning, applied here a second time).
- **Catch only `BrokenCircuitException` in `/callback`, not `Exception` broadly.** Considered, but a bare
  `Exception` catch is more defensible here: *any* exception escaping `AuthenticateAsync` has the identical
  problem (no `AuthenticationProperties`, no safe `returnUrl`, and a browser that must not see a raw 500 or
  OpenIddict's own error text) — narrowing the catch would only reintroduce that exact failure mode for
  whatever exception type isn't named.

## Related decisions

Resurrects ADR 0017's design, specifically rebuilt for the two call sites that exist after ADR 0035 removed
the ones it originally guarded (and the clean interface layer `IIdentityProvider`/`IExternalTokenVerifier`
it decorated, which no longer exists). Guards ADR 0026's token exchange and ADR 0029's BFF proxy. Reopens,
narrowly, the Polly v7/v8 packaging question ADR 0026 first navigated and ADR 0035 later closed by removing
the only thing that needed it.
