using OpenEmrNext.AuthFacade.Api;
using OpenEmrNext.AuthFacade.Api.Endpoints;

// Composition root: dependencies are registered on the builder, then the middleware and
// endpoints are mapped on the built app. Configuration is read from appsettings and the
// environment (OpenEmr:*, Jwt:*).
var builder = WebApplication.CreateBuilder(args);
builder.AddAuthFacadeHttpDependencies();
var app = builder.Build();
app.UseAuthFacadeHttpMiddleware();
app.MapAuthFacadeEndpoints();
app.Run();
