# 0034. The gateway preserves the original Host header for AuthFacade's routes

- **Status:** Accepted
- **Date:** 2026-11-01
- **Builds on:** [0026](0026-openiddict-client-drives-the-authorization-code-flow.md), [0033](0033-gateway-fronts-webapp-and-authfacade.md)

## Context

ADR 0033 left one thing explicitly unverified: a real browser completing the full login through the gateway.
The first real attempt failed with a server error on `/callback`:

```
InvalidOperationException: A grant type must be specified when triggering authentication demands from
endpoints that are not managed by the OpenIddict client stack. This error may also indicate that the
redirection endpoint was not correctly enabled in the OpenIddict client options.
```

The logs showed the `state` query parameter had already been looked up and the matching state-token row
updated — so OpenIddict.Client *had* found the in-flight login. The failure happened one step later, in
`ValidateAuthenticationDemand`, which also checks the current request against the registration's
`RedirectUri` (`http://localhost:7130/callback` — the gateway's address, per ADR 0033) before treating it as
a legitimate callback.

The cause: YARP, by default, builds the proxied request's `Host` header from the *destination's* own
address (`auth-facade:8080`), not the one the browser actually used (`localhost:7130`). AuthFacade's
`HttpContext.Request.Host` reflected the internal address, which didn't match the registered `RedirectUri`
— so OpenIddict treated `/callback` as an endpoint it didn't recognize, even though the state lookup, keyed
only by the `state` parameter and independent of `Host`, had already succeeded moments earlier.

## Decision

Added YARP's `RequestHeaderOriginalHost` transform to every route the gateway forwards to AuthFacade
(`OpenEmrNext.Gateway/Program.cs`), so the `Host` header the browser actually sent is preserved all the way
through to AuthFacade, instead of being replaced with the internal destination's own address.

## Consequences

- `/callback` (and anything else AuthFacade does that depends on knowing its own externally-visible address)
  now sees the correct `Host` regardless of being reached through the gateway.
- Verified two ways: a `curl`-driven run with a persistent cookie jar, through the real gateway → AuthFacade
  → OpenEMR chain, submitting a bogus authorization code — before the fix, this crashed with the error
  above; after it, it correctly reaches OpenEMR's own grant rejection and redirects to `?error=login_failed`,
  the designed-for failure path (ADR 0026). A real browser login was then confirmed to complete successfully,
  closing the one gap ADR 0033 left open.
- Scoped to AuthFacade's routes specifically, not WebApp's: static file serving has no notion of its own
  "registered address" to compare against, so there was nothing for it to get wrong there.
- This is specific to AuthFacade's own address-matching check; a different service added behind this gateway
  later is not guaranteed to need the same transform, but it is a reasonable first thing to check if it
  rejects a request that reached it correctly by path.

## Related decisions

Fixes the one unverified gap ADR 0033 recorded. Depends on ADR 0026's `EnableRedirectionEndpointPassthrough`
behavior, which is what performs the address comparison that failed.
