using OpenEmrNext.AuthFacade.Api;
using OpenEmrNext.AuthFacade.Api.OpenIddictClient;
using Scalar.AspNetCore;

// Composition root: dependencies are registered on the builder, then the middleware and
// endpoints are mapped on the built app. Configuration is read from appsettings and the
// environment (OpenEmr:*).
var builder = WebApplication.CreateBuilder(args);
builder.AddAuthFacadeHttpDependencies();
var app = builder.Build();
await app.EnsureOpenIddictClientDatabaseCreatedAsync();
app.UseAuthFacadeHttpMiddleware();
app.MapAuthFacadeEndpoints();

// The OpenAPI document and Scalar UI are for development only.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.Run();
