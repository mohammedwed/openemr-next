# Architecture decision records

Decisions that shape the system, one per file, numbered in order. A decision is
never edited to change its outcome; a new record supersedes the old one and the
old one's status is updated to point to it.

| ADR | Title | Status | Date |
|---|---|---|---|
| [0001](0001-authfacade-as-token-broker.md) | AuthFacade is a token broker in front of OpenEMR | Superseded by 0035 | 2026-10-10 |
| [0002](0002-facade-issued-tokens.md) | AuthFacade issues its own tokens | Superseded by 0013 | 2026-10-10 |
| [0003](0003-delegate-credential-verification-to-openemr.md) | Credential verification is delegated to OpenEMR | Superseded by 0035 | 2026-10-10 |
| [0004](0004-single-compose-and-configuration.md) | One Compose file and one configuration source | Accepted | 2026-10-11 |
| [0005](0005-oauth-client-provisioning-and-scopes.md) | The AuthFacade OAuth client is provisioned by seed with minimal scopes | Accepted | 2026-10-10 |
| [0006](0006-black-box-integration-tests.md) | Integration tests run black-box against the running stack | Accepted | 2026-10-11 |
| [0007](0007-layered-architecture-with-enforced-dependencies.md) | AuthFacade is split into layered projects with enforced dependency rules | Superseded by 0035 | 2026-10-21 |
| [0008](0008-login-credential-rules.md) | Login accepts any credentials OpenEMR could hold; OpenEMR decides correctness | Superseded by 0035 | 2026-10-21 |
| [0009](0009-password-plaintext-retrieved-once.md) | The password plaintext is retrieved once and cleared | Superseded by 0035 | 2026-10-21 |
| [0010](0010-separate-contracts-project.md) | AuthFacade HTTP contracts live in the API project; ports live in their own project | Accepted | 2026-10-23 |
| [0011](0011-host-neutral-composition-root.md) | Each host composes AuthFacade through a host-neutral composition root | Superseded by 0035 | 2026-10-23 |
| [0012](0012-accept-openemr-tokens-by-token-exchange.md) | AuthFacade accepts OpenEMR-issued JWTs through RFC 8693 token exchange | Superseded by 0013 | 2026-10-23 |
| [0013](0013-pass-openemr-tokens-through.md) | AuthFacade passes OpenEMR's token response through unchanged | Superseded by 0035 | 2026-10-23 |
| [0014](0014-password-grant-does-not-check-client-secret.md) | OpenEMR's password grant does not check the client secret | Superseded by 0035 | 2026-10-23 |
| [0015](0015-development-certificate-bypass-only-in-development.md) | The development certificate bypass is refused outside Development | Superseded by 0035 | 2026-10-23 |
| [0016](0016-facade-token-response-shape.md) | AuthFacade returns its own token response shape on login | Superseded by 0035 | 2026-10-24 |
| [0017](0017-circuit-breakers-guard-openemr-calls.md) | Circuit breakers guard calls to OpenEMR | Superseded by 0035 | 2026-10-24 |
| [0018](0018-rate-limit-the-login-endpoint-per-client-address.md) | The login endpoint is rate limited per client address | Superseded by 0035 | 2026-10-24 |
| [0019](0019-token-endpoint-binds-typed-form-parameters.md) | The token endpoint binds typed form parameters; binding failures are `invalid_request` | Superseded by 0035 | 2026-10-28 |
| [0021](0021-token-form-is-a-request-dto-with-attribute-validation.md) | The token form binds to a request DTO that validates its properties with attributes | Superseded by 0035 | 2026-10-28 |
| [0020](0020-local-stack-defaults-are-conservative.md) | The local stack is conservative by default: Production, loopback ports, non-root | Accepted | 2026-10-28 |
| [0022](0022-binder-failures-return-a-bare-400.md) | A form the binder cannot read returns a bare 400, not an OAuth body | Superseded by 0035 | 2026-10-29 |
| [0023](0023-token-endpoint-accepts-a-refresh-grant.md) | The token endpoint accepts a refresh grant, forwarded to OpenEMR | Superseded by 0035 | 2026-10-29 |
| [0024](0024-browser-authorization-code-flow-with-pkce.md) | Browser authorization-code flow with PKCE establishes a cookie session | Superseded by 0026 | 2026-10-30 |
| [0025](0025-seed-encrypts-the-client-secret.md) | The seed encrypts the AuthFacade client secret with OpenEMR's own CryptoGen | Accepted | 2026-10-30 |
| [0026](0026-openiddict-client-drives-the-authorization-code-flow.md) | OpenIddict.Client drives the browser authorization-code flow against OpenEMR | Accepted | 2026-10-30 |
| [0027](0027-persist-tokens-in-the-session-cookie.md) | The session cookie carries the real OpenEMR access and refresh token | Accepted | 2026-10-31 |
| [0028](0028-browser-login-and-session-assume-a-single-instance.md) | The browser login flow and the session cookie assume a single AuthFacade instance | Accepted | 2026-10-31 |
| [0029](0029-yarp-proxies-browser-sessions-to-openemrs-api.md) | YARP proxies a browser session's API calls to OpenEMR, with the real token attached server-side | Accepted; multi-service question resolved by 0030 | 2026-10-31 |
| [0030](0030-bff-proxy-forwards-to-the-gateway-not-each-service.md) | The BFF proxy forwards to the future gateway, not to each backend service directly | Accepted | 2026-10-31 |
| [0031](0031-nswag-client-generated-from-a-committed-snapshot.md) | A typed AuthFacade client is generated by NSwag from a committed OpenAPI snapshot | Accepted | 2026-10-31 |
| [0032](0032-webapp-is-a-wasm-frontend-hosted-by-authfacade.md) | The frontend is a Blazor WebAssembly app, hosted by AuthFacade itself | Superseded by 0033 | 2026-10-31 |
| [0033](0033-gateway-fronts-webapp-and-authfacade.md) | A gateway, not AuthFacade, gives WebApp and AuthFacade one shared origin | Accepted | 2026-10-31 |
| [0034](0034-gateway-preserves-the-original-host-header.md) | The gateway preserves the original Host header for AuthFacade's routes | Accepted | 2026-11-01 |
| [0035](0035-password-grant-and-bearer-auth-are-removed.md) | The password grant and bearer authentication are removed | Accepted | 2026-11-01 |
| [0036](0036-wait-for-openemrs-readiness-probe-at-startup.md) | AuthFacade waits for OpenEMR's readiness probe at startup | Accepted | 2026-11-01 |
| [0037](0037-authfacade-depends-on-seed-completing.md) | AuthFacade depends on the seed completing successfully | Accepted | 2026-11-01 |
| [0038](0038-rate-limit-login-and-callback-per-client-address.md) | The browser login flow is rate limited per client address | Accepted | 2026-11-01 |
| [0039](0039-gateway-terminates-tls-authfacade-trusts-forwarded-headers.md) | The gateway terminates TLS; AuthFacade trusts its forwarded headers | Accepted | 2026-11-01 |
| [0040](0040-rp-initiated-logout.md) | RP-initiated logout ends both AuthFacade's and OpenEMR's sessions | Accepted | 2026-11-01 |
