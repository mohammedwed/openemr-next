# 0022. A form the binder cannot read returns a bare 400, not an OAuth body

- **Status:** Superseded by [0035](0035-password-grant-and-bearer-auth-are-removed.md)
- **Supersedes:** points 3 and 4 of [0019](0019-token-endpoint-binds-typed-form-parameters.md), which gave an
  OAuth body to the framework's empty 400 through a middleware and endpoint marker

## Context

[0019](0019-token-endpoint-binds-typed-form-parameters.md) bound `grant_type`, `username` and `password` as typed
parameters whose static `TryParse` methods could fail. A failed parse was a framework-level binding failure that
left an empty 400 with no body, so `UseOAuthErrorBodies` filled it in with `{"error":"invalid_request"}` on
endpoints marked with `WithOAuthErrorBodies`. `AddOAuthErrorBodies` paired with it, setting
`RouteHandlerOptions.ThrowOnBadRequest` to `false` so the failure was a quiet 400 in every environment instead of
throwing in Development.

[0021](0021-token-form-is-a-request-dto-with-attribute-validation.md) replaced those typed parameters with
`PasswordLoginRequest`, whose fields are plain `string?`. A string cannot fail to bind. Every case the middleware
was written for — a missing, empty, too-long or otherwise invalid field — is now caught by
`WithRequestValidation`'s endpoint filter, which already writes a full `invalid_request` body before the
middleware would ever see an empty response. No test exercises the middleware producing output; the unit tests
for it only check the `AddOAuthErrorBodies`/`UseOAuthErrorBodies` startup pairing, not a request that reaches it.

The only way the framework can still fail to bind this form is a request body it cannot read at all as a form —
malformed `multipart`/url-encoded bytes, not an invalid field value. That is a different, much rarer failure than
the one the middleware and its marker metadata were built for, and does not need an OAuth-shaped body: it is a
transport-level failure, not a rejected OAuth request.

## Decision

1. **The middleware, the marker and `WithOAuthErrorBodies` are removed.** `OAuthErrorBodies.cs`,
   `AsPublicTokenEndpoint`'s call to it, and `UseAuthFacadeHttpMiddleware`'s call to the middleware are gone. A
   form the binder cannot read now returns a bare 400 with no body, the same shape a non-form request already
   gets as a 415.
2. **The `ThrowOnBadRequest` setting is kept, inlined into `AddAuthFacadeHttpDependencies`.** It is one line of
   service configuration with no middleware to pair with and no ordering hazard to guard, so it no longer needs
   its own file or startup check.
3. **`invalid_request` now has one source.** `WithRequestValidation`'s filter is the only place that produces it,
   for the only case that still occurs: an `AsParameters`-bound field present on the request but failing its
   validation attributes.

## Consequences

- `OAuthErrorBodiesTests` is deleted; it tested the startup-ordering guard between the middleware and the service
  setting, and that pairing no longer exists.
- A hand-crafted request with an unreadable form body gets a bare 400 instead of an OAuth-shaped one. No test
  covered that body before this change, and no realistic OAuth client sends a form it cannot encode correctly.
- `TokenEndpoints`'s `Produces<ErrorResponse>(StatusCodes.Status400BadRequest)` still describes the common case
  (the filter's `invalid_request`, and the handler's `invalid_grant`); the rare unreadable-body case is bodyless,
  the same way the already-documented 415 is.

## Alternatives considered

- **Keep the middleware for the unreadable-body case alone.** Rejected. It would keep a middleware, a marker and
  cross-file wiring in place for a failure mode with no test and no evidence a real client triggers it, to shape
  a body for a transport-level failure that isn't really an OAuth error response.
