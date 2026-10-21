using OpenEmrNext.AuthFacade.Api;

// Composition root: services are registered on the builder, then the middleware and
// endpoints are mapped on the built app. Configuration is read from appsettings and the
// environment (OpenEmr:*, Jwt:*).
var builder = WebApplication.CreateBuilder(args);
builder.AddAuthFacadeApi();
var app = builder.Build();
app.UseAuthFacadeApi();
app.Run();
