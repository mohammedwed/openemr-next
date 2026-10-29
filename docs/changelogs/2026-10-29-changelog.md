# Changelog 2026-10-29

## Summary

A form the binder cannot read now gets a bare 400 instead of an OAuth-shaped body from a middleware built for a
binding failure that no longer occurs. The decision is recorded in ADR 0022, which supersedes points 3 and 4 of
ADR 0019. The token endpoint also accepts a refresh grant now, so a client can redeem the refresh token OpenEMR
already issues instead of asking the user to log in again when the access token expires (ADR 0023).

## Added

- **The token endpoint accepts `grant_type=refresh_token`.** See ADR 0023.
  - `GrantType` accepts `refresh_token` alongside `password`.
  - A new `RefreshToken` value object mirrors `Password`'s presence/length guard and one-time `TakeValue()`.
  - `IIdentityProvider.RefreshGrantAsync` is implemented by `OpenEmrIdentityProvider`, reusing the password
    grant's form-posting and token-parsing code. `CircuitBreakingIdentityProvider` shares the password grant's
    breaker pipeline, since both call the same OpenEMR endpoint.
  - `TokenRequest` (renamed from `PasswordLoginRequest`) implements `IValidatableObject`, so which fields are
    required depends on `grant_type`. The endpoint's filter needed no change: `Validator.TryValidateObject`
    already runs `IValidatableObject.Validate`.
  - `TokenGrantHandler` (renamed from `PasswordLoginHandler`) has two `HandleAsync` overloads, one per grant,
    sharing the same status-mapping and token-verification logic.
  - The refresh grant shares the password grant's rate-limit policy; no separate policy was added.
  - `TokenEndpoints` dispatches on `grant_type` with an exhaustive `switch`; an unhandled value throws instead
    of silently falling into the wrong branch.

## Removed

- **`OAuthErrorBodies`'s middleware, marker and `WithOAuthErrorBodies`.** See ADR 0022.
  `PasswordLoginRequest`'s fields are plain strings and cannot fail to bind, so `WithRequestValidation`'s endpoint
  filter is now the only source of `invalid_request` on the token endpoint; the middleware never saw a case it
  needed to fill in. The `ThrowOnBadRequest = false` setting that keeps a genuine binder failure a quiet 400 in
  every environment is kept, inlined into `AddAuthFacadeHttpDependencies`.
- `OAuthErrorBodiesTests`, which tested the removed middleware's startup pairing with that setting.

## Changed

- **A request body the framework cannot parse as a form now returns a bare 400**, the same bodyless shape the
  already-documented 415 (non-form request) gets. Before, the middleware filled it in with
  `{"error":"invalid_request"}`.
- `AuthFacadeEndpoints.cs` moved from `Endpoints/` to the Api project's root, beside `AuthFacadeApiExtensions.cs`.
  `Program.cs` calls both directly as peer composition-root steps; only the file's location changed.
- **The OpenAPI document's form schema requires only `grant_type`.** `username`, `password` and
  `refresh_token` are each required only for their own grant, which a flat schema cannot express; the real
  rule is in `TokenRequest.Validate` and the operation description.

## Tests

- Unit tests: 205 pass, up from 178. New tests cover `RefreshToken`, the refresh grant's `GrantType` values,
  `TokenRequest`'s conditional validation, and `TokenGrantHandler`'s refresh-grant cases.
- Architecture tests: 12 pass. Integration tests: 14 pass against the rebuilt stack, including a live test that
  mints a real refresh token via password grant and redeems it against OpenEMR.
- Not covered as a dedicated live test: an invalid or missing refresh token reaching OpenEMR. That behavior is
  unit-tested and shares the same OpenEMR-rejection and local-validation code paths the password-grant live
  tests already exercise; adding it would have pushed the suite over the shared login rate limit the file's
  tests count against.
