# 0019. The token endpoint binds typed form parameters; binding failures are `invalid_request`

- **Status:** Superseded by [0035](0035-password-grant-and-bearer-auth-are-removed.md); point 1 was already superseded by [0021](0021-token-form-is-a-request-dto-with-attribute-validation.md); points 3-4 by [0022](0022-binder-failures-return-a-bare-400.md)
- **Date:** 2026-10-28
- **Supersedes:** the `unsupported_grant_type` code in point 4 of [0016](0016-facade-token-response-shape.md)

## Context

The token endpoint read the form inside its handler, checked the grant type, parsed the username and password with the value objects' `TryParse` methods, and returned `400 unsupported_grant_type` or `400 invalid_request` itself. That put request validation, orchestration and result mapping in one method.

ASP.NET Core can do the binding. A parameter marked `[FromForm]` is parsed from the form field of that name, and a type with a static `TryParse` is parsed with it. `GrantType`, `Username` and `Password` already have one. The framework reports a failed parse as an empty 400, and in Development it throws instead, which puts the exception text in the response body.

## Decision

1. **The endpoint takes the form fields as typed `[FromForm]` parameters.** They are `PasswordGrant`, `Username` and `Password`, bound by name with `[FromForm(Name = ...)]` because the OAuth field names are snake case. `PasswordGrant` accepts only `password` and delegates to `GrantType`, so there is one definition of what is supported. The handler runs only when all three parse.
2. **Any binding failure returns 400 `invalid_request`.** That covers a missing or invalid username or password, and a missing or unsupported grant type. `unsupported_grant_type` is no longer returned. The framework cannot say which parameter failed, and the distinction helped client developers only a little.
3. **The empty 400 gets an OAuth body from a small middleware.** `UseOAuthErrorBodies` writes `{"error":"invalid_request"}` for a 400 with no body on an endpoint marked with `WithOAuthErrorBodies`. Responses that already have a body, such as `invalid_grant`, are left alone.
4. **The middleware and a service setting are a pair.** `AddOAuthErrorBodies` sets `RouteHandlerOptions.ThrowOnBadRequest` to false in every environment. `UseOAuthErrorBodies` throws at startup if the setting is missing, so the two cannot drift apart silently.
5. **A request that is not a form returns 415 with no body.** Routing rejects it before the endpoint is chosen, so the middleware cannot mark it.
6. **Route conventions are grouped.** `AsPublicTokenEndpoint` applies anonymous access, no antiforgery, the error bodies and the login rate limit of [0018](0018-rate-limit-the-login-endpoint-per-client-address.md) together.
7. **Error bodies come from `ErrorResponse` factories.** Each OAuth code and its fixed description is spelled once.
8. **The documented endpoints are named.** `IssueToken` and `GetCurrentUser` become the OpenAPI `operationId`, and a test pins both names.

## Consequences

- The handler holds no validation. The value objects keep the rules of [0008](0008-login-credential-rules.md) and [0009](0009-password-plaintext-retrieved-once.md), and the framework applies them.
- Clients that read `unsupported_grant_type` must read `invalid_request` instead. No clients depend on it today.
- A non-form request now gets a bare 415. Before, it got a 400 with a body.
- The behaviour depends on how ASP.NET Core reports binding failures. The tests pin the responses, so a framework change that alters them fails the suite.
- The framework logs a failed binding at Debug level with the submitted value. For a password that happens only when it is empty or over 1024 characters.
- The OpenAPI generator describes a parameter by its C# type, so the value objects appeared as objects (`username` with a `value` property, `password` with none). Clients built from the document, including the Scalar UI, then sent something the endpoint did not parse as a username, and logins failed with `invalid_grant`. A schema transformer (`DescribeFormFieldsAsStrings`) describes `PasswordGrant`, `Username` and `Password` as strings, with their length limits and `format: password`, and tests pin the schemas.
- Each new grant type needs its own parameter shape. When the form grows, `BindAsync` on a request type is the next step, because it controls the snake case names and the errors.

## Alternatives considered

- **Keep parsing in the handler, then move it to `TokenRequest.TryParse`.** Built, then replaced. It kept the endpoint's input out of its signature, and the handler still parsed by hand.
- **`[FromBody]`.** Rejected. It reads JSON only, and a token endpoint must accept `application/x-www-form-urlencoded`. A form post gets 415.
- **A complex `[FromForm]` type.** Rejected for now. The binder matches property names, so `grant_type` does not map to `GrantType`, and `[FromForm(Name = ...)]` on a property is ignored. Properties of a type with only `TryParse` fail with a 500, and would need `IParsable<T>` on the Domain types.
- **Keep `unsupported_grant_type`.** Rejected. It needed a way to tell which parameter failed, and the framework gives none.
- **A `BindAsync` result type.** Rejected for now. It keeps both codes and the framework still binds, but it adds a type for two error branches. It is the planned route if the form grows.
