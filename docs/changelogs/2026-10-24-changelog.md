# Changelog 2026-10-24

## Summary

AuthFacade returns its own token response on login instead of OpenEMR's body, and refused logins return a standard `invalid_grant`. The OpenEMR identity provider is guarded by a circuit breaker. The decision is recorded in ADR 0016, which supersedes point 1 of ADR 0013.

## Added

- **Circuit breakers on OpenEMR calls.** See ADR 0017.
  - `CircuitBreakingIdentityProvider` wraps `IIdentityProvider`. Counted failures are transport errors, timeouts, unusable success responses, and OpenEMR 408 and 5xx. While the circuit is open, login returns 503 without calling OpenEMR.
  - `CircuitBreakingTokenVerifier` wraps `IExternalTokenVerifier`. It counts only an unreachable JWKS endpoint. A token that fails verification does not count.
  - Both use `Polly.Core` 8.8.0. Each state is shared across requests through one singleton pipeline.
  - Thresholds come from `OpenEmr:CircuitBreaker` (`FailureRatio`, `SamplingDuration`, `MinimumThroughput`, `BreakDuration`). The defaults are 50%, at least 5 calls in 30 seconds, and a 30-second break. Startup refuses out-of-range values.
- **`TokenResponse`** contract in the API project, with `access_token`, `token_type`, and `expires_in` and `refresh_token` when known.
- **`OpenEmrToken`** port type, carrying the four fields the provider reads from OpenEMR's success response.

## Changed

- **Login success returns AuthFacade's token response.** It replaces OpenEMR's body, which was passed through unchanged under ADR 0013. Other fields in OpenEMR's response are dropped. A missing `token_type` is `Bearer`.
- **Refused logins return 400 `invalid_grant`** with the description `The username or password is incorrect.` OpenEMR's body is no longer forwarded.
- **An OpenEMR 5xx or 408 on login returns 502 `server_error`.** Before, the same status was passed through to the client.
- **An OpenEMR timeout is treated as unreachable.** It returns 503 `server_error`. Before, it escaped as an unhandled error.
- **`PasswordGrantResult`** carries the parsed `OpenEmrToken` instead of the raw body, and `LoginRejected` carries no fields. `LoginSucceeded` carries the token.
- The Composition README's consumption guide shows the new login outcomes.

## Tests

- Unit tests: 122 pass, up from 91. Integration tests: 13 pass against the rebuilt stack. New tests cover the decorator's failure rules, the timeout mapping, the 400 and 502 mappings, `TokenResponse`'s field names and optional fields, and the provider's token parsing.
