# 0012. AuthFacade accepts OpenEMR-issued JWTs through RFC 8693 token exchange

- **Status:** Superseded by [0013](0013-pass-openemr-tokens-through.md). The token exchange grant is removed; the JWKS verification in points 2 and 3 is kept and now serves bearer validation.
- **Date:** 2026-10-23

## Context

Users who already hold an access token issued by OpenEMR need to call AuthFacade without logging in again. ADR 0002 says AuthFacade issues its own tokens, and the bearer scheme accepts only tokens signed with `Jwt:Key`, so an OpenEMR token cannot be used directly.

ADR 0003 delegates credential verification to OpenEMR, but that covers passwords. A token is a different input: AuthFacade has to check it locally before trusting it. The README already warns against reading JWT claims without checking the signature.

OpenEMR's discovery document at `/oauth2/default/.well-known/openid-configuration` publishes the issuer (`https://<host>/oauth2/default`) and a `jwks_uri`. Its tokens are RS256 and carry `iss`, `aud` (the OAuth client ID, `authfacade` in this stack), `sub` and `exp`.

## Decision

1. **Exchange, not pass-through.** AuthFacade accepts an OpenEMR JWT on its existing `/connect/token` endpoint using the RFC 8693 token exchange grant: `grant_type=urn:ietf:params:oauth:grant-type:token-exchange`, with `subject_token` and `subject_token_type=urn:ietf:params:oauth:token-type:jwt`. The response is a facade-issued token, as ADR 0002 requires. The OpenEMR token is not returned to the caller.
2. **Verify before trusting.** The signature is checked against the keys at `jwks_uri`. The token must also pass these checks, using the settings in `OpenEmr:*`:
   - `iss` equals `OpenEmr:Issuer`.
   - `aud` equals `OpenEmr:ClientId`.
   - `exp` and `nbf` are within the allowed clock skew.
   - The signing algorithm is RS256. Other algorithms, including `none`, are rejected.
3. **Keys come from JWKS under `OpenEmr:BaseUrl`.** Keys are fetched from `{BaseUrl}/oauth2/default/jwk` and cached for an hour. The discovery document's `jwks_uri` is not used: it names the public host (`https://localhost:9300`), which the AuthFacade container cannot reach. A token whose signature does not verify triggers one refresh, at most once every five minutes, so key rotation is picked up without letting bad tokens force a fetch on every request.
4. **Name is not carried over.** OpenEMR's access token and `id_token` in this stack carry no `name` or `preferred_username`, and userinfo returned nothing with the granted scope. The exchanged token carries the subject and an empty name. A later decision can add a name if OpenEMR's scopes supply one.
5. **Failure mapping.** A token that fails verification returns 400 `invalid_grant`. A JWKS or discovery fetch that fails returns 503 `server_error`, as the login path does for an unreachable OpenEMR.

## Consequences

- AuthFacade has a second way to obtain its own tokens, and both ways end at the same issuer.
- One new required setting: `OpenEmr:Issuer`, the issuer string OpenEMR writes into its tokens. `OpenEmr:BaseUrl` is where keys are fetched. The two differ inside the Docker network, so they are separate settings. `OpenEmr:ClientId` doubles as the expected audience.
- AuthFacade depends on OpenEMR being reachable for key retrieval. Keys are cached, so a short outage does not fail verified tokens.
- The verification logic lives in Infrastructure behind a port in Application.Ports, so the use case stays free of JWT details.

## Alternatives considered

- **Pass the OpenEMR token through unchanged.** Rejected. It breaks ADR 0002, and the bearer scheme cannot validate it.
- **Token introspection.** Rejected for now. It needs a call to OpenEMR for every exchange. JWKS verification works offline between key refreshes.
- **A shared signing key.** Rejected. It couples two systems' secrets and cannot be rotated independently.
- **Read the claims without verifying the signature.** Rejected. The README forbids it for client-supplied tokens.
