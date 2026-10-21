# 0003. Credential verification is delegated to OpenEMR

- **Status:** Accepted
- **Date:** 2026-10-10
- **Source:** Converted from `docs/changelogs/2026-10-10-changelog.md` (decision 2)

## Context

An earlier version verified passwords by reading OpenEMR's `users` and `users_secure` tables and checking BCrypt hashes in the facade.

## Decision

AuthFacade does not read user tables or verify password hashes. It forwards the credentials to OpenEMR's OAuth2 server, which applies its own rules.

## Consequences

- MFA, lockout and password-expiry rules apply without being copied. A copy would drift from OpenEMR's rules.
- There is one credential store, not two.
- AuthFacade cannot enforce its own lockout or throttling. Any such control must be added in front of it or in OpenEMR.
- Login decisions depend on OpenEMR's responses. Its status codes and error bodies are passed through, as [0008](0008-login-credential-rules.md) describes.

## Alternatives considered

- **Verify hashes in the facade.** Rejected. It duplicates login logic without MFA, lockout or expiry, and it creates a second credential store.
