# 0058. PatientManagement's OpenAPI document declares the gateway secret and reflects validation attributes

- **Status:** Accepted
- **Date:** 2026-11-15
- **Builds on:** [0031](0031-nswag-client-generated-from-a-committed-snapshot.md) (the `AuthFacadeOpenApi` transformer pattern reused here), [0055](0055-patientmanagement-api-gated-by-a-shared-secret-identity-forwarding-deferred.md)

## Context

`PatientManagementApiExtensions` already called `builder.Services.AddOpenApi()`, and `Program.cs`
already maps `/openapi/v1.json` and Scalar's UI in Development -- but nobody had checked the document
itself was actually correct since. Fetching the real generated document (running the app with
placeholder config, the same way AuthFacade's own `generate-authfacade-openapi.sh` does) surfaced two
real gaps:

1. **No security scheme at all.** `security: null` on every operation, no `X-Internal-Gateway-Secret`
   declared anywhere in `components.securitySchemes` -- ADR 0055's gate added a real `[Authorize]`-
   equivalent requirement to every endpoint, but the OpenAPI document never learned about it. Scalar's
   "Try it" UI would have no way to know to send the header; a client generated from this document
   would have no way to know the call needs one at all.
2. **DataAnnotations never reached the schema.** `RegisterPatientRequest`'s generated schema showed no
   `minLength`/`maxLength`/`format`/`minimum` for any of its `[StringLength]`/`[Range]`/`[EmailAddress]`
   attributes, despite `AddValidation()` (the same session's earlier work) genuinely enforcing them at
   runtime. Confirmed why, not just that: `JsonPropertyInfo.AttributeProvider` only reflects the
   auto-generated property, but every one of these DTOs is a positional record with the attributes on
   the primary constructor's *parameter*, not a `[property: ...]`-targeted property -- so the attribute
   never reaches that provider. `JsonPropertyInfo.AssociatedParameter.AttributeProvider` does carry it.
3. **A specific, confirmed-not-assumed precision bug along the way.** Writing `RangeAttribute.Maximum`
   straight through for `[Range(1, long.MaxValue)]` produced `"maximum": 9.223372036854776E+18` in the
   actual document -- not the real `9223372036854775807`. Root cause, confirmed by reflecting the
   attribute directly: `RangeAttribute` has no `(long, long)` constructor, only `(int, int)` and
   `(double, double)`, so `[Range(1, long.MaxValue)]` resolves to the double overload and the bound is
   already lossy by the time any OpenAPI code ever sees it. Every `[Range]` in these DTOs uses
   `long.MaxValue` as a "must be positive" idiom anyway, never a real upper bound, so the fix is to omit
   a known-wrong maximum rather than publish it.

## Decision

1. **`PatientManagementOpenApi.ConfigurePatientManagement()`** (`Api/OpenApi/`), wired via
   `AddOpenApi(options => options.ConfigurePatientManagement())`, mirrors `AuthFacadeOpenApi`'s own
   pattern exactly: a document transformer sets the title/description and declares `InternalGateway` as
   an `apiKey`/header scheme named `X-Internal-Gateway-Secret`; an operation transformer adds that
   scheme as a security requirement on every operation whose endpoint metadata carries `IAuthorizeData`
   -- which, since ADR 0055 put `.RequireAuthorization()` on both route groups, is every operation in
   this service, unlike AuthFacade where only `/me` needs it.
2. **`ValidationAttributesSchemaTransformer`** (`Api/OpenApi/`), registered via
   `options.AddSchemaTransformer<T>()`, reads each property's attributes from both
   `JsonPropertyInfo.AttributeProvider` and `JsonPropertyInfo.AssociatedParameter.AttributeProvider` (the
   union covers both a positional record's parameter-held attributes and, defensively, any future DTO
   that uses an explicit `[property: ...]` target instead) and sets `MinLength`/`MaxLength` from
   `StringLengthAttribute`, `Minimum` from `RangeAttribute`, and `Format = "email"` from
   `EmailAddressAttribute`. This only decorates the schema for documentation/client-generation purposes
   -- `AddValidation()` and the Domain factories remain the actual enforcement, unaffected by it.
3. **`RangeAttribute.Maximum` is only written when it's a real bound.** A small `IsRealUpperBound` check
   rejects any `double` maximum that is `>= long.MaxValue` -- the signature of the lossy-double-overload
   sentinel these DTOs' `[Range(1, long.MaxValue)]` attributes all produce. `Minimum` is always written;
   none of these DTOs' minimums are near a precision boundary.
4. **A new `OpenApiDocumentTests`** (`IntegrationTests`, same `WebApplicationFactory<Program>` pattern
   every other Api-level test here already uses) proves all of this against the real generated document,
   not just by reasoning about the transformer code: the title/description, the scheme's exact shape,
   that literally every operation in the document requires it, and that `RegisterPatientRequest`'s
   schema carries the right `minLength`/`maxLength`/`format`/`minimum` and *no* `maximum` key at all.

## Consequences

- Scalar's "Try it" UI (and any OpenAPI-driven client, if one is ever generated the way AuthFacade's
  `OpenEmrNext.AuthFacade.Client` is) now knows every call needs `X-Internal-Gateway-Secret`, and gets
  real field constraints instead of a shape with no bounds at all.
- The `RangeAttribute` double-overload precision issue is a real, independently-true fact about
  `[Range(1, long.MaxValue)]`'s runtime behavior too, not just an OpenAPI-document cosmetic: the
  attribute's own `Maximum` is already a double, not the exact `long.MaxValue`, before any validation
  ever runs. In practice this changes nothing -- no `long` value can ever exceed `long.MaxValue`
  regardless of how the upper bound is internally represented -- but it's worth knowing if a future
  change ever needs an exact 64-bit upper bound tighter than "effectively none."
- No typed client or committed OpenAPI snapshot exists for PatientManagement, unlike AuthFacade's own
  NSwag-generated `OpenEmrNext.AuthFacade.Client` (ADR 0031). Building one is a separate, larger decision
  this ADR does not make.

## Alternatives considered

- **Leave the missing security scheme undocumented**, relying on callers already knowing the header
  from ADR 0055's own text. Rejected: the whole point of a generated API document is that a caller
  shouldn't need to have read an ADR first; AuthFacade already solved this exact problem for its own
  scheme, and the pattern was directly reusable.
- **Write the real, precise `long.MaxValue` as `Maximum` by formatting it manually** (bypassing
  `RangeAttribute.Maximum`'s own lossy double, e.g. hard-coding the string `"9223372036854775807"`
  whenever the attribute's value looks like the double-rounded sentinel). Rejected: these `[Range]`
  attributes were never expressing a real upper bound in the first place (they exist only to reject
  non-positive ids) -- publishing a precise-looking but semantically meaningless upper bound would be
  more misleading than omitting it, not less.
- **A build-time static OpenAPI snapshot** (`Microsoft.Extensions.ApiDescription.Server`), matching the
  stated goal of ADR 0031's own eventual client-generation pipeline. Not pursued now: this task was
  about the document's live correctness, not about generating a typed client; a snapshot-based pipeline
  is a reasonable, separate future step if a PatientManagement client is ever needed.

## Related decisions

Reuses the transformer pattern [0031](0031-nswag-client-generated-from-a-committed-snapshot.md)
established for AuthFacade's own `AuthFacadeOpenApi`. Makes [0055](0055-patientmanagement-api-gated-by-a-shared-secret-identity-forwarding-deferred.md)'s
gate actually discoverable from the document it should always have been reflected in.
