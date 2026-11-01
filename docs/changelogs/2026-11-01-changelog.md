# Changelog 2026-11-01

## Summary

The first real browser login attempt through the gateway (merged the day before, ADR 0033) failed on
`/callback` with an OpenIddict error that looked like a configuration mistake but was a proxying one: YARP
rewrites the `Host` header to the internal destination by default, and AuthFacade's redirection-endpoint
check depends on seeing the browser's actual one. Fixed and verified end to end — the one gap ADR 0033 left
open is now closed.

## Fixed

- **The gateway now preserves the original `Host` header** on every route it forwards to AuthFacade
  (`OpenEmrNext.Gateway/Program.cs`, via YARP's `RequestHeaderOriginalHost` transform). Without it,
  AuthFacade saw `Host: auth-facade:8080` instead of `Host: localhost:7130`, so OpenIddict.Client's check
  that `/callback` matches its registered `RedirectUri` failed — even though the `state` parameter lookup
  moments earlier had already found the in-flight login correctly, since that lookup does not depend on
  `Host` at all. See ADR 0034.

## Tests

- Verified with `curl` and a persistent cookie jar through the real gateway → AuthFacade → OpenEMR chain,
  submitting a bogus authorization code: before the fix, `/callback` crashed with
  `InvalidOperationException: ... endpoints that are not managed by the OpenIddict client stack`; after it,
  the same request correctly reaches OpenEMR's own grant rejection and redirects to `?error=login_failed`,
  the designed-for failure path (ADR 0026).
- A real browser login was then confirmed to complete successfully end to end through the gateway.
