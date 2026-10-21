# 0002. AuthFacade issues its own tokens

- **Status:** Accepted
- **Date:** 2026-10-10
- **Source:** Converted from `docs/changelogs/2026-10-10-changelog.md` (decision 3, and the security notes)

## Context

Validating OpenEMR's tokens inside AuthFacade meant fetching OpenEMR's signing keys and trusting its self-signed certificate inside the JWT handler. That failed with "signature key not found" and made startup fragile.

## Decision

After OpenEMR accepts the login, AuthFacade reads the subject from OpenEMR's token and signs its own token: HS256, issuer `openemr-next`, audience `openemr-next-api`, 30-minute lifetime. Clients validate only AuthFacade's tokens and never receive OpenEMR's.

The subject is read without verifying the signature. This is acceptable only because the token arrives directly from OpenEMR over TLS in the same request. The pattern must not be reused for client-supplied tokens.

## Consequences

- `/me` uses one simple validation path.
- Tokens have no refresh, revocation or key rotation yet.
- The signing key is a shared secret held in configuration. Key size is enforced at startup.

## Alternatives considered

- **Pass OpenEMR's token through to clients.** The first version did this. It was replaced so that clients never hold the upstream token.
- **Validate OpenEMR's tokens against its JWKS.** Rejected because of the signature-key failure above.
