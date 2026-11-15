# 0055. PatientManagement.Api is gated by a shared secret; forwarding the real caller's identity is deferred

- **Status:** Accepted
- **Date:** 2026-11-15
- **Builds on:** [0048](0048-authfacade-stays-the-bridge-gateway-validation-deferred.md), [0030](0030-bff-proxy-forwards-to-the-gateway-not-each-service.md), [0049](0049-patient-management-writes-into-the-legacy-database.md)

## Context

PatientManagement.Api had no authentication or authorization at all: no `UseAuthentication()`/
`UseAuthorization()`, no `[Authorize]`, nothing. Every endpoint trusted whatever identity a caller
supplied in the request body or query string (`RegisterPatientRequest.RegisteredBy`,
`UploadAdvanceDirectiveDocumentRequest.OwnerId`, `SetAdvanceDirectiveStatusRequest.AuthenticatorUserId`,
`FhirPatientEndpoints.CreatePatient`'s `registeredBy` query parameter) with zero verification. This was
a deliberate, named deferral: ADR 0048 explains there is no bearer token on this path to validate yet,
and the gateway (ADR 0033) makes no auth decisions of any kind.

The ask this session was to close this properly: gate the service shut, and forward the real
authenticated user's identity instead of trusting caller-supplied fields. The second half turns out not
to be buildable today:

- AuthFacade's session cookie carries exactly one claim worth anything, `sub` — and it is OpenEMR's own
  UUID (`users.uuid`), not a numeric id. Every claim on the OIDC `id_token` is copied through unchanged
  (`OpenIddictClientEndpoints.HandleCallbackAsync`); there is no claim-transformation step anywhere in
  this codebase, and the project's own `/me` endpoint and changelogs confirm OpenEMR's tokens in this
  stack carry no `name`/`preferred_username` either — `sub` really is the only usable claim.
- `RegisteredBy`, `OwnerId`, and `AuthenticatorUserId` map directly onto real legacy integer columns —
  `patient_data.created_by`, `documents.owner`, `patient_data.advance_directive_user_authenticator` —
  with no column-type override anywhere in `PatientManagementDbContext`/`EfAdvanceDirectiveRepository`
  (confirmed by reading both). These are `users.id`-shaped integer foreign keys in legacy's own schema
  (ADR 0049: this service maps onto `patient_data`/`documents` as they already exist, and does not own
  or migrate that schema), not strings.
- There is no lookup anywhere in this system from OpenEMR's UUID to its numeric `users.id`. Building one
  is a real, open-ended question — does OpenEMR's own API even expose a users-by-UUID lookup? — that
  neither this session's research nor anything already built here answers. Guessing at a shape for it
  risks a rewrite once the real answer is known, the same concern ADR 0048 itself named for gateway
  token validation.

So real identity-forwarding needs its own future ADR, once that lookup question has an actual answer.
What's fully grounded today is narrower: stop the service from being open to anyone at all.

## Decision

1. **A new required option, `InternalGatewayOptions.Secret`** (`PatientManagement.Composition`, bound
   and `ValidateOnStart()`-validated the same way `PatientManagementDbOptions` already is), holds a
   shared secret.
2. **A new authentication handler, `InternalGatewayAuthenticationHandler`** (`PatientManagement.Api`,
   modeled directly on the now-deleted `OpenEmrBearerHandler` — ADR 0035 — recovered from git history for
   its shape): reads the `X-Internal-Gateway-Secret` header, compares it to the configured secret with
   `CryptographicOperations.FixedTimeEquals` (constant-time, so a network-adjacent caller can't learn the
   secret byte-by-byte from response timing), and succeeds or fails accordingly. No header at all is
   `AuthenticateResult.NoResult()`, matching `OpenEmrBearerHandler`'s own "no credential presented, not a
   failed one" distinction.
3. **Every endpoint requires it**: `.RequireAuthorization()` on both route groups
   (`PatientManagementEndpoints`'s `/api/v1` group, `FhirPatientEndpoints`'s `/fhir` group) — two
   one-line changes, no edits needed to any individual endpoint method.
4. **Caller-supplied identity fields are unchanged.** `RegisteredBy`/`OwnerId`/`AuthenticatorUserId` are
   still read from the request exactly as before. This is not a new gap — it's the same one ADR 0048
   already named — only now it requires a caller that knows the shared secret, not anyone on the network.
5. **No caller attaches this header yet.** PatientManagement isn't wired into docker-compose (no
   Dockerfile, no service block) or into the Gateway's routes (confirmed: `Gateway/Program.cs` has
   exactly two clusters, `authfacade` and `webapp`, nothing else). Per ADR 0030, the Gateway — not a
   direct AuthFacade cluster — is the intended owner of routing to new backend services; it is also the
   natural place to eventually resolve a caller's real identity and attach it alongside this secret, once
   M3 actually builds that route. Wiring that is a separate, larger piece of M3 work, not bundled here.

## Consequences

- PatientManagement.Api can no longer be called by anyone who doesn't know the configured secret —
  closing it to the open internet, independent of the identity question.
- The identity-forwarding gap is now explicitly tracked (this ADR), not silently dropped. A future ADR
  closing it will need to answer the UUID→numeric-id question first, then extend
  `InternalGatewayAuthenticationHandler` (or replace it) to also place a resolved, trustworthy user id
  somewhere the four affected endpoints can read instead of the request body/query string.
- Every PatientManagement integration test that drives the real HTTP pipeline
  (`PatientEndpointTests`, `FhirPatientEndpointTests`, `InsuranceEndpointTests`,
  `AdvanceDirectiveEndpointTests`) now configures the same test secret and attaches it to every request;
  a new `GatewaySecretAuthenticationTests` proves the gate itself — no header, or the wrong one, is
  rejected with 401.
- `appsettings`-level configuration still doesn't exist for this host (none ever has); the secret is
  supplied purely through environment-variable/config-provider binding, same as every other
  PatientManagement option. Wiring an actual `.env`/docker-compose value is deferred along with the
  Gateway routing itself (see point 5).

## Alternatives considered

- **Resolve a numeric user id now, via some guessed lookup against OpenEMR.** Rejected: no such lookup
  is confirmed to exist; building one on a guess risks a rewrite once the real answer is known — the
  same reasoning ADR 0048 already applied to gateway token validation.
- **Store OpenEMR's UUID directly in `created_by`/`owner`/`advance_directive_user_authenticator`.**
  Rejected: these are real legacy integer FK columns this service maps onto as-is (ADR 0049); storing
  a UUID there is a type mismatch, not a C#-level change.
- **Add a direct AuthFacade→PatientManagement cluster now, bypassing the Gateway.** Rejected: ADR 0030
  already settled this — new backend services are reached through the Gateway, not a growing route table
  on AuthFacade's own BFF proxy.

## Related decisions

Extends, rather than reopens, [0048](0048-authfacade-stays-the-bridge-gateway-validation-deferred.md)'s
deferral. Respects [0030](0030-bff-proxy-forwards-to-the-gateway-not-each-service.md)'s routing-ownership
rule. Keeps [0049](0049-patient-management-writes-into-the-legacy-database.md)'s schema-mapping
approach intact by not reshaping `created_by`/`owner`/`advance_directive_user_authenticator`.
