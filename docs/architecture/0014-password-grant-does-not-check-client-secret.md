# 0014. OpenEMR's password grant does not check the client secret

- **Status:** Accepted
- **Date:** 2026-10-23

## Context

The facade sends `client_id` and `client_secret` on every password grant, and it stores the secret in `OpenEmr:ClientSecret`. The `authfacade` client is registered as confidential (`is_confidential = 1`).

Tests against the running stack on 2026-10-23 showed that OpenEMR accepts the password grant with a wrong secret and with no secret. An unknown `client_id` is rejected with 401 `invalid_client`.

OpenEMR's source explains this. `ClientRepository::validateClient()` in `src/Common/Auth/OpenIDConnect/Repositories/ClientRepository.php` checks the secret only for the `authorization_code` grant. For the password and refresh grants it returns `true` without checking the secret.

OpenEMR's client-credentials grant does not use a shared secret. It authenticates the client with a signed JWT assertion. The `authfacade` client has no JWKS and no `client_credentials` in `grant_types`, so that grant is refused.

OpenEMR is an unmodified upstream image (ADR 0001), so this behavior cannot be changed from the facade.

## Decision

1. **The facade treats `client_id` as public, not as a credential.** It does not rely on the secret to protect the password grant. Anyone who knows a registered, enabled client ID can send password grants to OpenEMR.
2. **The facade keeps sending the secret.** The secret does nothing on this grant, but sending it costs nothing, and it will matter once OpenEMR checks it or the facade moves to the authorization-code flow.
3. **No app-level token is cached.** Caching needs an app-level call to reuse it, and the facade makes none. Client credentials would also need a key-pair assertion setup, not a shared secret (see the alternatives).

## Consequences

- Login's only protection is OpenEMR's user checks: password, lockout, MFA. The facade's client secret protects nothing on this path.
- The README and the composition guide must not describe the secret as protecting login.
- The facade's protection against misuse by another client is limited to the set of enabled clients in OpenEMR. Keep that set small.
- The real fix is either an OpenEMR change, which ADR 0001 rules out for now, or the authorization-code flow with PKCE, which is ADR 0001's production target. PKCE makes OpenEMR check the secret for confidential clients.

## Alternatives considered

- **Cache an app-level client-credentials token.** Deferred. OpenEMR would need `client_credentials` in `grant_types` and a registered JWKS, and nothing in the facade would use the token yet.
- **Patch OpenEMR's `validateClient()` to check the secret for the password grant.** Rejected for now. It forks upstream and breaks ADR 0001.
- **Disable the password grant in OpenEMR.** Not checked. OpenEMR's configuration may have an option for this, which would remove the unchecked path. Needs a separate investigation.
