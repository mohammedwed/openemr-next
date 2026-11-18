using OpenEmrNext.PatientManagement.Api;
using OpenEmrNext.PatientManagement.Api.Authentication;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddPatientManagementHttpDependencies();
var app = builder.Build();

// Catches anything an endpoint's own catch blocks don't -- see AddProblemDetails in
// PatientManagementApiExtensions for why this alone (no custom IExceptionHandler) is enough.
app.UseExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();

// A verified user on every /api/v1 and /fhir route (ADR 0075), checked before any body is read.
app.UseVerifiedUser();
app.UseIdempotency();

app.MapPatientManagementEndpoints();

// The OpenAPI document and Scalar UI are for development only, matching AuthFacade's own convention.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.Run();
