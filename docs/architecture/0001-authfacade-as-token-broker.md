# 0001. AuthFacade is a token broker in front of OpenEMR

- **Status:** Superseded by [0035](0035-password-grant-and-bearer-auth-are-removed.md)
- **Date:** 2026-10-10 (confirmed 2026-10-11)
- **Source:** Converted from `docs/changelogs/2026-10-10-changelog.md` (decisions 2, 4) and `docs/changelogs/2026-10-11-changelog.md` (decision 3)

## Context

Clients need to sign in through OpenEMR. OpenEMR already owns users, access control and password hashing. Rebuilding those would duplicate a security-critical surface, and OpenEMR must stay an unmodified, separately deployed system.

## Decision

AuthFacade takes a username and password, performs an OAuth2 password grant against OpenEMR's token endpoint, and issues its own short-lived JWT. It stores no passwords and no user records. OpenEMR's token never leaves the service boundary. The OpenEMR integration sits behind one adapter, `OpenEmrIdentityProvider`.

## Consequences

- Identity stays in OpenEMR, so its login rules (MFA, lockout, expiry) apply unchanged. See [0003](0003-delegate-credential-verification-to-openemr.md).
- The password grant is deprecated in OAuth 2.1. It is kept only for this integration. The production target is the browser authorization-code flow with PKCE, which is not implemented.
- AuthFacade is the token issuer that clients see. Its tokens are described in [0002](0002-facade-issued-tokens.md).
- Failures inside OpenEMR, such as its failed-login path returning 500, surface through AuthFacade and cannot be fixed from outside.

## Alternatives considered

- **OpenIddict server.** Dropped. It requires EF Core stores in the OpenEMR database, which the project chose not to change.
- **Direct verification of hashes in OpenEMR's `users` table.** Rejected. See [0003](0003-delegate-credential-verification-to-openemr.md).

## Related decisions

[0013](0013-pass-openemr-tokens-through.md) supersedes the statement above that AuthFacade issues its own short-lived JWT and that OpenEMR's token never leaves the service boundary. AuthFacade now returns OpenEMR's token response unchanged.

[0012](0012-accept-openemr-tokens-by-token-exchange.md) adds a second adapter to OpenEMR, `OpenEmrTokenVerifier`, which verifies OpenEMR-issued tokens against its JWKS for the token exchange path. OpenEMR's token still does not leave the service boundary.
