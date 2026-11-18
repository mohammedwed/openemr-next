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
| [0028](0028-browser-login-and-session-assume-a-single-instance.md) | The browser login flow and the session cookie assume a single AuthFacade instance | Accepted; 2 of 3 gaps closed by 0043 | 2026-10-31 |
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
| [0041](0041-circuit-breakers-guard-the-remaining-openemr-calls.md) | Circuit breakers guard the remaining calls to OpenEMR | Accepted | 2026-11-01 |
| [0042](0042-audit-log-scoped-to-login-and-the-bff-proxy.md) | The audit trail covers login outcomes and the BFF proxy, not `/me` | Accepted | 2026-11-01 |
| [0043](0043-authfacade-session-state-in-a-dedicated-mariadb-database.md) | AuthFacade's session state persists in a dedicated MariaDB database | Accepted | 2026-11-01 |
| [0044](0044-session-store-backs-the-cookie-for-revocation.md) | A server-side session store backs the cookie, so a session can be revoked | Accepted | 2026-11-01 |
| [0045](0045-silent-refresh-and-admin-session-revocation.md) | Silent token refresh, and admin-initiated session revocation | Accepted | 2026-11-02 |
| [0046](0046-persisted-openiddict-certificates.md) | OpenIddict's encryption and signing certificates are persisted files | Accepted | 2026-11-02 |
| [0047](0047-the-identity-bridge-design.md) | The identity bridge: AuthFacade's design, end to end | Accepted | 2026-11-02 |
| [0048](0048-authfacade-stays-the-bridge-gateway-validation-deferred.md) | AuthFacade stays the identity bridge; the gateway's token validation is deferred | Accepted | 2026-11-13 |
| [0049](0049-patient-management-writes-into-the-legacy-database.md) | Patient Management writes into OpenEMR's own database, not a new one | Accepted | 2026-11-13 |
| [0050](0050-m4-testing-and-database-access.md) | M4 uses Testcontainers, and a least-privilege database user of its own | Accepted | 2026-11-13 |
| [0051](0051-m4-scope-expands-to-full-legacy-parity-minus-portal.md) | M4 scope expands to full legacy patient parity, minus the portal | Accepted | 2026-11-13 |
| [0052](0052-firely-net-fhir-sdk-replaces-hand-rolled-fhir-mapping.md) | The Firely .NET SDK replaces the hand-rolled FHIR Patient mapping, and adds US Core validation | Accepted | 2026-11-14 |
| [0053](0053-insurancecoverage-aggregate-cluster-for-the-swap-operation.md) | InsuranceCoverage is a multi-entity aggregate cluster, owning the swap decision | Accepted | 2026-11-14 |
| [0054](0054-domain-readmodels-is-a-separate-project.md) | Domain's read models move into their own project, Domain.ReadModels | Accepted | 2026-11-14 |
| [0055](0055-patientmanagement-api-gated-by-a-shared-secret-identity-forwarding-deferred.md) | PatientManagement.Api is gated by a shared secret; forwarding the real caller's identity is deferred | Accepted | 2026-11-15 |
| [0056](0056-patient-management-retries-transient-database-failures.md) | Patient Management's database connection retries transient failures | Accepted | 2026-11-14 |
| [0057](0057-patient-management-adds-global-exception-handling-middleware.md) | Patient Management adds global exception-handling middleware | Accepted | 2026-11-14 |
| [0058](0058-patientmanagement-openapi-declares-the-gateway-secret-and-reflects-validation-attributes.md) | PatientManagement's OpenAPI document declares the gateway secret and reflects validation attributes | Accepted | 2026-11-15 |
| [0059](0059-nswag-generates-a-typed-patientmanagement-client.md) | NSwag generates a typed PatientManagement client from a committed OpenAPI snapshot | Accepted | 2026-11-15 |
| [0060](0060-patientmanagement-is-deployed-and-reachable-through-the-gateway.md) | PatientManagement is deployed and reachable through the Gateway; WebApp gets a first frontend slice | Accepted | 2026-11-15 |
| [0061](0061-webapp-covers-insurance-and-advance-directives.md) | WebApp's frontend extends to insurance and advance directives | Accepted | 2026-11-15 |
| [0062](0062-webapp-chrome-flattens-toward-the-plain-blazor-template.md) | WebApp's chrome flattens toward the plain Blazor template's look | Accepted | 2026-11-15 |
| [0064](0064-clinical-care-shares-the-legacy-database-and-coexists-with-legacy-encounter-writers.md) | Clinical Care shares the legacy database and coexists with the legacy encounter writers | Accepted | 2026-11-16 |
| [0065](0065-vitals-are-stored-in-us-units-and-the-api-takes-explicit-units.md) | Vitals are stored in US units, and the API takes and returns explicit units | Accepted | 2026-11-16 |
| [0066](0066-patient-registered-reaches-clinical-care-through-an-outbox-relay-over-http.md) | PatientRegistered reaches Clinical Care through an outbox relay over HTTP, with a secret of its own | Accepted | 2026-11-16 |
| [0067](0067-webapp-covers-encounters-and-vitals.md) | WebApp's frontend extends to encounters and vitals | Accepted | 2026-11-17 |
| [0068](0068-clinical-care-updates-are-conditional-on-last-update-and-touch-only-owned-columns.md) | Clinical Care updates are conditional on the row's last-update stamp and touch only the columns they own | Accepted | 2026-11-17 |
| [0069](0069-clinical-care-audits-writes-to-its-own-append-only-table.md) | Clinical Care audits its writes in its own append-only table | Proposed | 2026-11-17 |
| [0070](0070-clinical-care-owns-the-problem-and-allergy-issue-types-in-the-shared-lists-table.md) | Clinical Care owns the problem and allergy issue types in the shared `lists` table | Accepted | 2026-11-17 |
| [0071](0071-webapp-covers-problems-allergies-and-issue-links.md) | WebApp's frontend extends to problems, allergies and their links to encounters | Accepted | 2026-11-17 |
| [0072](0072-immunizations-are-versioned-by-content-because-the-table-has-no-usable-stamp.md) | Immunizations are versioned by content, because their table has no usable stamp | Accepted | 2026-11-17 |
| [0073](0073-soap-notes-are-addressed-by-their-form-id-within-their-encounter.md) | SOAP notes are addressed by their form id within their encounter | Accepted | 2026-11-17 |
| [0074](0074-clinical-notes-get-their-own-parent-form-allocated-under-a-database-lock.md) | Clinical notes get their own parent form, its id allocated under a database lock | Accepted | 2026-11-17 |
| [0075](0075-the-gateway-validates-the-session-and-forwards-the-verified-user.md) | The gateway validates the session and forwards the verified user to Clinical Care and Patient Management | Accepted | 2026-11-17 |
| [0076](0076-deleting-records-needs-legacys-admin-super-right-and-is-a-flag-not-a-removal.md) | Deleting vitals, SOAP notes and clinical notes needs legacy's admin/super right, and is a flag, not a removal | Accepted | 2026-11-17 |
| [0077](0077-creating-requests-accept-an-idempotency-key-and-soap-notes-guard-their-own-retries.md) | Creating requests accept an Idempotency-Key, and a SOAP note guards its own retry | Accepted | 2026-11-17 |
| [0078](0078-patient-management-keeps-its-own-append-only-audit-trail.md) | Patient Management keeps its own append-only audit trail | Accepted | 2026-11-17 |
