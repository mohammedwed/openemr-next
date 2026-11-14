# 0056. Patient Management adds global exception-handling middleware

- **Status:** Accepted
- **Date:** 2026-11-14
- **Builds on:** `docs/m4-domain-notes.md`'s Resilience and failing fast dimension, [0055](0055-patient-management-retries-transient-database-failures.md)

## Context

The same Resilience and failing fast review that led to ADR 0055 found a second gap: no global
exception-handling middleware anywhere in `OpenEmrNext.PatientManagement.Api`. Every endpoint already
catches its own known Domain validation exceptions inline and maps them to a clean
`Results.ValidationProblem`/`Results.NotFound` response -- but anything unanticipated (a transient
database error that exhausts ADR 0055's retries, a real bug) fell through to ASP.NET Core's bare
default 500, with no structured body, unlike every other error path in this API.

Confirmed, not assumed, that this was a repo-wide gap, not specific to this service: neither
`AuthFacade` nor `Gateway` have `UseExceptionHandler`/`ProblemDetails` configured either. Deprioritized
below the ADR 0055 retry fix at the time (a response-shaping concern, not a correctness/availability
one), then picked up immediately after.

## Decision

Add the minimal built-in ASP.NET Core pattern, no custom `IExceptionHandler`:

```csharp
// PatientManagementApiExtensions.AddPatientManagementHttpDependencies
builder.Services.AddProblemDetails();
```
```csharp
// Program.cs, before app.MapPatientManagementEndpoints()
app.UseExceptionHandler();
```

With `IProblemDetailsService` registered (`AddProblemDetails()`) and no custom handler or exception
path configured, ASP.NET Core's own `ExceptionHandlerMiddleware` already logs the exception at Error
level and writes a `ProblemDetails` body for whatever status code is current (500, since nothing set
it otherwise) -- no hand-rolled handler class needed to get that behavior.

Every endpoint's own inline exception handling for known validation exceptions is unchanged. This
middleware only ever sees what wasn't already caught closer to where it happened.

## Consequences

- Verified against the real HTTP pipeline, not just by reasoning about the framework API: a new test,
  `GlobalExceptionHandlingTests`, swaps in an always-throwing `IPatientFhirRepository` via
  `ConfigureTestServices` (the real `WebApplicationFactory<Program>`, not a synthetic minimal host) and
  confirms `GET /fhir/Patient/{uuid}` returns a 500 with an `application/problem+json` body, not a bare
  empty response. All 46 pre-existing integration tests still pass unchanged, confirming every
  endpoint's own specific exception handling still takes precedence correctly.
- This closes the Resilience and failing fast dimension's two findings. The dimension itself stays
  open for future review the same way the other five do.
- `AuthFacade` and `Gateway` still don't have this -- fixing it for them, if ever done, is a separate,
  explicitly out-of-scope decision, not implied by this one.

## Alternatives considered

- **A custom `IExceptionHandler`** mapping specific exception types to specific status codes (e.g.,
  `KeyNotFoundException` -> 404) at the middleware level, rather than per-endpoint. Rejected: every
  endpoint already does this inline, consistently, and duplicating that mapping at a second layer would
  create two places that could drift apart. The global handler's job here is narrower and clearer:
  catch what nothing else anticipated, not re-implement what already works.
- **Leave it as tracked debt, same as it was initially.** Rejected once picked back up: unlike ADR
  0055's retry fix, this change carries no transactional-correctness risk to weigh against -- it's two
  lines, verified by a real test, with no plausible way to make an already-failing request worse.
