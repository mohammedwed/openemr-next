# Changelog 2026-11-15

## Summary

A grounded architecture-dimension review of `OpenEmrNext.PatientManagement.Api` (all 19 files), the
last of the four Patient Management projects reviewed this way (Domain, Application/Application.Ports,
Infrastructure, and now Api). Three findings, all fixed.

## Fixed — three Api findings

- **Documentation**: the "caller-supplied identity, not verified against any authenticated session --
  an already-accepted gap inherited from ADR 0048" caveat previously only lived on
  `RegisterPatientRequest.RegisteredBy`. Added the same note to
  `UploadAdvanceDirectiveDocumentRequest.OwnerId`, `SetAdvanceDirectiveStatusRequest.AuthenticatorUserId`,
  and `FhirPatientEndpoints.CreatePatient`'s `registeredBy` query parameter — the same pattern, now
  documented consistently everywhere it appears. Not a new security finding: the lack of
  authentication itself is ADR 0048's own explicit, named deferral.
- **SOLID**: `PatientEndpoints.RegisterPatient` and `FhirPatientEndpoints.CreatePatient` each
  independently constructed a `PatientRegistration` and called `RegisterPatientUseCase.ExecuteAsync` —
  duplicated orchestration across two Api files. Fixed with an additive convenience overload on
  `RegisterPatientUseCase.ExecuteAsync` that accepts raw primitive fields and constructs the
  registration internally, leaving the existing `PatientRegistration`-accepting overload untouched
  (several integration tests use it directly). Each endpoint keeps shaping its own error response
  (REST `ValidationProblem` vs. FHIR `OperationOutcome`) — only the construction call moved.
- **Security (defense-in-depth)**: `FhirProfileValidator.ExtractIfNeeded` extracted FHIR profile
  packages into a fixed, predictable temp-directory path, gated only by a marker file's existence, not
  its content. Fixed by making the cache content-addressed — the destination subdirectory and marker
  are both keyed on the source `.tgz`'s SHA-256 hash, so a changed package version can never be
  silently shadowed by a stale cache, and skipping extraction now requires matching that exact hash.
- Also found and fixed in the same pass: 2 dead `using` statements in `FhirPatientEndpoints.cs`
  (`Domain.Fhir`, `Domain.Fhir.Rules`) that the earlier Application-layer SOLID cleanup round missed.

## Added — DataAnnotations on every Request DTO

- `[Required]`/`[StringLength]`/`[Range]`/`[EmailAddress]` added to all 5 `Contracts/Requests`
  types, enforced by `builder.Services.AddValidation()` (newly wired in
  `PatientManagementApiExtensions.cs`). Every `ErrorMessage` matches the equivalent Domain factory's
  own wording where one exists; new messages were written only for fields with no Domain
  counterpart. Closes the one genuine prior gap: `SetAdvanceDirectiveStatusRequest.AuthenticatorUserId`
  had no validation anywhere before this. Covered by a new integration test,
  `PUT_status_with_a_non_positive_authenticator_id_returns_400`.

## Cleaned up — Api code organization

- New `ValidationProblems` helper (`Endpoints/ValidationProblems.cs`) replaces 8 duplicated inline
  `Results.ValidationProblem(...)` constructions across `PatientEndpoints`, `InsuranceEndpoints`, and
  `AdvanceDirectiveEndpoints`.
- Removed `FhirPatientMapper`'s duplicate `UsCoreProfile` constant in favor of the existing
  `FhirProfileValidator.UsCorePatientProfile`.
- Removed 4 more dead `using` statements (`Domain.Insurance.Rules`, `Domain.Insurance.ReadModels`,
  `Domain.AdvanceDirectives.ReadModels`, and a bare `Domain` in `FhirPatientEndpoints.cs`) that the
  earlier SOLID cleanup round missed; each verified safe by removal + full rebuild.

## Audited — hardcoded values across PatientManagement

- Swept `Domain`, `Application`, `Application.Ports`, `Infrastructure`, `Composition`, and `Api` for
  literals that should be configuration. Everything found was already a bound options default or a
  legacy-grounded Domain invariant with its source cited in-code -- no new findings there.
- One real finding, left open for a decision: `new MariaDbServerVersion(new Version(12, 3, 3))` is
  duplicated identically in `PatientManagementApiExtensions.cs` and the test fixture
  `PatientDataContainerFixture.cs` (plus a third, independent copy in AuthFacade, out of this
  service's scope). Already acknowledged in-code as a deliberate repo-wide pin, not an oversight --
  not fixed here pending a decision on whether to deduplicate the two `PatientManagement` copies.

## Added — shared-secret gate on PatientManagement.Api (ADR 0055)

- Every endpoint now requires the `X-Internal-Gateway-Secret` header to match a configured secret
  (`InternalGatewayOptions.Secret`), enforced by a new `InternalGatewayAuthenticationHandler` (modeled
  on the deleted `OpenEmrBearerHandler`, ADR 0035) wired via `.RequireAuthorization()` on both route
  groups. A request without it, or with the wrong value, gets 401.
- This closes the "anyone can call this service" half of the gap ADR 0048 named. The other half --
  `RegisteredBy`/`OwnerId`/`AuthenticatorUserId` still being caller-supplied, not resolved from a real
  authenticated identity -- is unchanged and not fixed here. Doing that requires resolving OpenEMR's
  UUID `sub` claim to a numeric `users.id`, which nothing in this codebase does today; ADR 0055 tracks
  this explicitly as a follow-up rather than leaving it undocumented.
- All four HTTP-pipeline integration test fixtures (`PatientEndpointTests`, `FhirPatientEndpointTests`,
  `InsuranceEndpointTests`, `AdvanceDirectiveEndpointTests`) now configure a test secret and attach it
  to every request. New `GatewaySecretAuthenticationTests` proves the gate itself: no header or the
  wrong header both return 401.

## Fixed — PatientManagement's OpenAPI document (ADR 0058)

- The document declared no security scheme at all, despite every endpoint now requiring the
  gateway secret (ADR 0055). Fixed with `PatientManagementOpenApi.ConfigurePatientManagement()`
  (mirrors AuthFacade's own `AuthFacadeOpenApi`): declares `X-Internal-Gateway-Secret` as an
  `apiKey`/header scheme and requires it on every operation.
- DataAnnotations never reached the generated schema (`RegisterPatientRequest` showed no
  `minLength`/`maxLength`/`format`/`minimum` despite real attributes). Root cause: these are
  positional records, so the attributes live on the constructor parameter, not the property
  `JsonPropertyInfo.AttributeProvider` reflects. Fixed with a new
  `ValidationAttributesSchemaTransformer` that also checks `AssociatedParameter.AttributeProvider`.
- Found and fixed along the way: `[Range(1, long.MaxValue)]`'s `Maximum` was writing as a
  precision-losing `9.223372036854776E+18`, not the real `9223372036854775807` — `RangeAttribute`
  has no `(long, long)` constructor, so the bound is already a lossy double inside the attribute
  itself. Since every `[Range]` here is a "must be positive" idiom, the fix omits the wrong
  maximum rather than publish it.
- Covered by a new `OpenApiDocumentTests`, verified against the real generated document.

## Tests

Full solution builds clean (0 errors, same 5 pre-existing unrelated nullable warnings). Full
PatientManagement suite run and passing: 54 integration tests (including the new gateway-secret
and OpenAPI-document tests), 121 unit tests, 17 architecture tests.
