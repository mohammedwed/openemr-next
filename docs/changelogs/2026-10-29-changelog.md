# Changelog 2026-10-29

## Summary

A form the binder cannot read now gets a bare 400 instead of an OAuth-shaped body from a middleware built for a
binding failure that no longer occurs. The decision is recorded in ADR 0022, which supersedes points 3 and 4 of
ADR 0019.

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

## Tests

- Unit tests: 178 pass. Architecture tests: 12 pass.
