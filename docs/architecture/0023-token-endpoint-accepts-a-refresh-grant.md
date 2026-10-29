# 0023. The token endpoint accepts a refresh grant, forwarded to OpenEMR

- **Status:** Accepted
- **Date:** 2026-10-29

## Context

Until now the token endpoint accepted only the password grant, so a client had to resend the username and
password every time its access token expired. OpenEMR already issues a refresh token on login: the
`authfacade` OAuth client holds the `offline_access` scope ([0005](0005-oauth-client-provisioning-and-scopes.md)),
and `OpenEmrIdentityProvider` already parsed a `refresh_token` out of OpenEMR's response and passed it through
in `TokenResponse` ([0016](0016-facade-token-response-shape.md)). Nothing on the facade could redeem it.

Two things were checked live against the running stack before building this:

1. **OpenEMR issues a refresh token for this client.** A password-grant login returns one.
2. **OpenEMR accepts a refresh grant for this client despite its `grant_types` column.** The seeded
   `oauth_clients` row lists only `authorization_code` ([0005](0005-oauth-client-provisioning-and-scopes.md)'s
   known gap), and the seed does not reconcile it. Posting `grant_type=refresh_token` straight to OpenEMR's own
   token endpoint with a token from step 1 returned 200 with a new access token anyway. This is the same skip
   [0014](0014-password-grant-does-not-check-client-secret.md) already found for the password grant's client
   secret, extended to the refresh grant's `grant_types` check. No OpenEMR-side change was needed.

## Decision

1. **`GrantType` accepts `refresh_token`.** Its pattern becomes `(password|refresh_token)`.
2. **A `RefreshToken` value object mirrors `Password`.** Presence and a 4096-character length guard only;
   OpenEMR decides whether the token is still valid. `TakeValue()` returns the plaintext once and clears it,
   the same guard [0009](0009-password-plaintext-retrieved-once.md) put on `Password`: a refresh token is as
   much a bearer secret as a password, since holding it is enough to mint a new access token.
3. **`IIdentityProvider` gets `RefreshGrantAsync`.** `OpenEmrIdentityProvider` implements it by posting
   `grant_type=refresh_token` and reusing the same form-posting and token-parsing code the password grant uses.
   It returns the same `PasswordGrantResult` shape; that type was never password-specific, just "OpenEMR's
   status plus its token."
4. **`CircuitBreakingIdentityProvider` shares its existing pipeline across both grants.** Both call the same
   OpenEMR token endpoint, so an outage should open the same circuit regardless of which grant tripped it.
5. **`PasswordLoginHandler` becomes `TokenGrantHandler`, with two `HandleAsync` overloads.** One takes a
   `Username`/`Password`, the other a `RefreshToken`; both funnel into the same private method that submits the
   grant, maps OpenEMR's status, and verifies a successful token. `LoginResult` and its four cases were already
   grant-agnostic and needed no change.
6. **`PasswordLoginRequest` becomes `TokenRequest` and implements `IValidatableObject`.** `GrantType` stays
   unconditionally `[Required]`. Which of `Username`/`Password` or `RefreshToken` is required depends on
   `GrantType`'s value, which a fixed attribute cannot express; `Validate()` enforces that instead.
   `Validator.TryValidateObject(..., validateAllProperties: true)` already calls `IValidatableObject.Validate`,
   so `WithRequestValidation`'s filter needed no change. Each field's shape (`StringLength`, the username's
   `RegularExpression`) stays a fixed, unconditional attribute: those attributes already treat `null` as valid
   by convention, so they no-op on whichever field the submitted grant type does not use, and the OpenAPI
   document keeps describing their real length limits.
7. **The OpenAPI document's `required` list is now just `grant_type`.** `Username`, `Password` and
   `RefreshToken` are no longer unconditionally required, and a flat JSON Schema object cannot express
   "required depending on a sibling field's value" without a bigger `oneOf` restructuring, which this change
   does not take on.
8. **The refresh grant shares the password grant's rate-limit policy.** `AsPublicTokenEndpoint` applies one
   `RequireRateLimiting(LoginPolicy)` to the whole endpoint, unchanged. No separate policy was added.

## Consequences

- A client refreshing frequently from one address shares the same per-address budget
  ([0018](0018-rate-limit-the-login-endpoint-per-client-address.md)) as password-guessing attempts from that
  same address. A legitimate client refreshing often, or several users behind one NAT'd address, can be
  rate-limited by a budget sized for credential guessing, not refresh traffic. Accepted for now; a separate
  policy is the next step if this proves to matter in practice.
- `PasswordGrantResult` keeps its name even though it is returned by both grants. Renaming it was out of scope
  for this change.
- The rules are still written twice for `Username`/`Password` (DTO attributes and value-object checks, accepted
  by [0021](0021-token-form-is-a-request-dto-with-attribute-validation.md)); `RefreshToken` now carries the same
  duplication, with its own `TokenRequestTests` pinning agreement at the boundaries.
- The OpenAPI document no longer states that `username`/`password` are required, which is now actually true:
  the real rule lives only in `TokenRequest.Validate` and the endpoint's description text, not the schema.

## Alternatives considered

- **A separate `RefreshTokenRequest` DTO and a second mapped route.** Rejected. Minimal APIs route one request
  type per endpoint; two DTOs would need either two routes (not possible for the same path and method) or a
  custom `BindAsync` dispatcher, which is more machinery than one DTO with conditional validation.
- **A separate rate-limit policy for the refresh grant.** Deferred, not rejected outright; see Consequences.
- **A separate `RefreshTokenLoginHandler` beside `PasswordLoginHandler`.** Rejected. The status-mapping and
  token-verification logic is identical for both grants; duplicating it would drift the two over time.
