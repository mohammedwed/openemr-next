using OpenEmrNext.ClinicalCare.Api.Authentication;
using OpenEmrNext.ClinicalCare.Api;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddClinicalCareHttpDependencies();
var app = builder.Build();

app.UseExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();

// A verified user on every /api/v1 route (ADR 0075), checked before any body is read.
app.UseVerifiedUser();
app.UseIdempotency();

app.MapClinicalCareEndpoints();

// The OpenAPI document and Scalar UI are for development only, matching the other services.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.Run();
