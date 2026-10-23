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

## Later decision

[0012](0012-accept-openemr-tokens-by-token-exchange.md) accepts the alternative above for one path only: an OpenEMR JWT presented in an RFC 8693 token exchange is verified against OpenEMR's JWKS, and the caller receives an AuthFacade token. The failure above came from fetching keys from the `jwks_uri` in OpenEMR's discovery document, which names the public host, and from trusting OpenEMR's self-signed certificate implicitly. ADR 0012 fetches keys from the configured base URL and makes certificate trust an explicit setting. This decision still holds: clients receive only AuthFacade tokens.
