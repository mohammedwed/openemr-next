using OpenEmrNext.PatientManagement.Api;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddPatientManagementHttpDependencies();
var app = builder.Build();

// Catches anything an endpoint's own catch blocks don't -- see AddProblemDetails in
// PatientManagementApiExtensions for why this alone (no custom IExceptionHandler) is enough.
app.UseExceptionHandler();

app.MapPatientManagementEndpoints();

// The OpenAPI document and Scalar UI are for development only, matching AuthFacade's own convention.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.Run();
