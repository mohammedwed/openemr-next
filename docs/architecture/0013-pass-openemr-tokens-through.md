# 0013. AuthFacade passes OpenEMR's token response through unchanged

- **Status:** Superseded by [0035](0035-password-grant-and-bearer-auth-are-removed.md); point 1 was already superseded by [0016](0016-facade-token-response-shape.md)
- **Date:** 2026-10-23
- **Supersedes:** [0002](0002-facade-issued-tokens.md), and the token exchange in [0012](0012-accept-openemr-tokens-by-token-exchange.md)

## Context

ADR 0002 has AuthFacade sign its own HS256 token after OpenEMR accepts a login. That token hides OpenEMR's token response from clients: its refresh token and expiry never reach them. It also adds a second token format, a shared signing secret in `Jwt:Key`, and a key-rotation problem. ADR 0012 added a token exchange so that callers holding an OpenEMR token could get a facade token, which only adds one more step to the same design.

The project's direction is to pass along what OpenEMR offers, not to add a layer of tokens on top of it.

## Decision

1. **Login returns OpenEMR's token response unchanged, once its access token verifies.** `POST /connect/token` with a password grant returns OpenEMR's status and body. On rejection, that is OpenEMR's error. On success, it is OpenEMR's token response, including whatever fields it returns (`access_token`, `token_type`, `expires_in`, and `refresh_token` if issued). Before returning it, AuthFacade verifies the `access_token` with the same verifier `/me` uses (point 3). The rules are:
   - the body is JSON with a non-empty `access_token`, otherwise 502;
   - the token fails verification, otherwise 502, because a token OpenEMR issued that does not verify is an upstream fault;
   - the JWKS keys cannot be fetched, so the token cannot be checked, otherwise 503.
2. **AuthFacade issues no tokens.** It has no signing key, no `Jwt:*` settings, and no HS256 path.
3. **Bearer validation accepts OpenEMR's tokens.** `GET /me` reads the `Authorization: Bearer` credential and verifies it with `IExternalTokenVerifier` (ADR 0012's `OpenEmrTokenVerifier`). The checks are RS256 only, the signature against OpenEMR's JWKS, issuer equal to `OpenEmr:Issuer`, audience equal to `OpenEmr:ClientId`, and the lifetime. The verified `sub` and, if present, `name` claims form the principal. The verifier is called from `OpenEmrBearerHandler` in the API project.
4. **The token exchange grant is removed.** A caller that already holds an OpenEMR token sends it as a bearer credential to `/me`. Exchanging it for another token has no purpose now.

## Consequences

- Clients receive OpenEMR's tokens, so OpenEMR's token lifetime, refresh tokens and revocation apply. This reverses ADR 0001's statement that OpenEMR's token never leaves the service boundary, and ADR 0002's rejected alternative of passing the token through.
- Refresh goes to OpenEMR's token endpoint. AuthFacade has no refresh route of its own.
- Login depends on OpenEMR's JWKS as well as its token endpoint. Keys are cached for an hour, so a short JWKS outage does not fail logins that have a cached key.
- Key rotation is handled by the JWKS refresh in ADR 0012, with its one-hour cache and five-minute cooldown after a signature failure. The shared HS256 secret and its rotation problem are gone.
- `/me` depends on OpenEMR's JWKS for verification. If the keys cannot be fetched, the handler refuses the token, so `/me` returns 401 rather than 503. An authentication handler cannot return a 503 for this case.
- `/me` verifies the signature on each request, using cached keys. This costs some CPU per request.
- The `name` claim is forwarded if OpenEMR supplies one. OpenEMR's tokens in this stack have none, so `/me` returns a null name until OpenEMR supplies it (ADR 0012, point 4).
- Settings: `OpenEmr:BaseUrl`, `OpenEmr:Issuer`, `OpenEmr:ClientId` and `OpenEmr:ClientSecret` remain required. `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience` and `Jwt:LifetimeMinutes` are removed, along with the `JWT_KEY` environment variable.

## Alternatives considered

- **Keep facade-issued tokens (ADR 0002).** Rejected. It hides OpenEMR's token response and adds a second token format and secret.
- **Keep the token exchange alongside pass-through.** Rejected. Its only purpose was to produce a facade token, so it has no use once the facade issues none.
- **Token introspection against OpenEMR on each `/me` call.** Not chosen now. It adds a network call per request, while JWKS verification works offline between key refreshes.
