# 0021. The token form binds to a request DTO that validates its properties with attributes

- **Status:** Superseded by [0035](0035-password-grant-and-bearer-auth-are-removed.md)
- **Date:** 2026-10-28
- **Supersedes:** point 1 of [0019](0019-token-endpoint-binds-typed-form-parameters.md), which bound the form fields as typed parameters with `TryParse`

## Context

[0019](0019-token-endpoint-binds-typed-form-parameters.md) bound `grant_type`, `username` and `password` as three typed parameters. Each type's static `TryParse` decided whether a field was valid. The rules therefore lived in three places (`GrantType`, `Username`, `Password`), and the endpoint's signature was built from domain types. The OpenAPI document needed a schema transformer to describe those domain types as strings.

0019 rejected a complex `[FromForm]` type because the binder matches property names, so `[FromForm(Name = ...)]` on a property is ignored and `grant_type` cannot map to a property. That is still true for `[FromForm]`. `[AsParameters]` binds each constructor parameter by its own attributes, so `[FromForm(Name = ...)]` works on a record's primary constructor.

## Decision

1. **The token endpoint takes one request DTO, `PasswordLoginRequest`, in `Api/Contracts/Requests/`.** It is bound with `[AsParameters]`. Each constructor parameter carries `[FromForm(Name = ...)]` with the OAuth field name, and each property carries `[JsonPropertyName]` and its validation attributes (`Required`, `StringLength`, `RegularExpression`, `AllowedValues`).
2. **A validation filter checks the attributes.** `WithRequestValidation<PasswordLoginRequest>()` adds an endpoint filter. It validates the request argument with `Validator.TryValidateObject` and returns `400 invalid_request` on failure, so the handler receives only valid requests. The handler builds the value objects with `new Username(...)` and `new Password(...)` from that input.
3. **The rules are the rules of [0008](0008-login-credential-rules.md).** Username is 1 to 255 characters with no control characters, and password is 1 to 1024 characters. `Required` uses `AllowEmptyStrings = true`, so whitespace-only values stay valid, as they were before. The value objects keep the same rules and still enforce them. A unit test checks that the attributes and the value objects agree at the boundaries. The value objects no longer have a `TryParse` method. Their constructors are the only way to build them, and they throw `ArgumentException` on invalid input.
4. **`PasswordGrant` is removed.** `GrantType.Password` is the constant `AllowedValues` checks, so the grant rule is still defined once in Domain.
5. **The OpenAPI document shows one object.** The generator emits `[AsParameters]` as an `allOf` of single-field schemas. `AuthFacadeOpenApi` merges them into one object with the three required fields, and marks `password` with `format: password`. `FormFieldSchemas` is removed, because the DTO's strings need no transformer.
6. **Wire behaviour is unchanged.** Responses, status codes, error bodies and serialized names are the same as under 0019.

## Consequences

- The endpoint's signature no longer names domain types. The form's rules sit with the form's fields.
- The rules are written twice, as attributes on the DTO and as checks in the value objects. The consistency tests catch drift at the boundaries, but a change to one side still needs the other.
- The password string lives in the DTO as a managed `string`, which cannot be cleared. [0009](0009-password-plaintext-retrieved-once.md) kept the plaintext in the value object only until it was taken. The DTO copy lasts until the request ends and is not cleared. `PasswordLoginRequest.ToString()` masks the password as `********`, so logging the request does not reveal it.
- The OpenAPI flattening depends on the generator's `allOf` shape for `[AsParameters]`. The document tests pin the result, so a generator change that alters it fails the suite.
- The OAuth error bodies from [0019](0019-token-endpoint-binds-typed-form-parameters.md) are still needed for framework binding failures, such as a form that cannot be read.

## Alternatives considered

- **A complex `[FromForm]` type with `[FromForm(Name)]` on the properties.** Rejected, as in 0019. The binder ignores the name on a property.
- **Keep the typed parameters and add `IParsable<T>`.** Rejected. It keeps the rules on the domain types and keeps the signature built from them, which is what this change moves away from.
- **`BindAsync` on the request type.** Still the planned route if the form grows. It is more code than `[AsParameters]`, and the form does not need it yet.
- **`AddValidation()` from ASP.NET Core 10.** Rejected. It returns a `ValidationProblem` body, not `invalid_request`, which would change the wire contract.
