using OpenEmrNext.AuthFacade.Api.Endpoints;
using OpenEmrNext.AuthFacade.Application;
using OpenEmrNext.AuthFacade.Infrastructure.OpenEmr;
using OpenEmrNext.AuthFacade.Infrastructure.Security;

namespace OpenEmrNext.AuthFacade.Api;

/// <summary>Wires the AuthFacade API. Called from Program and from in-process tests.</summary>
public static class AuthFacadeApiExtensions
{
    /// <summary>
    /// Registers the application services, the OpenEMR identity provider, service JWT
    /// issuance and validation, and authorization. Must run before the app is built.
    /// </summary>
    /// <param name="builder">The host builder to configure.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static WebApplicationBuilder AddAuthFacadeApi(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddApplication()
            .AddOpenEmrIdentityProvider(builder.Configuration, builder.Environment)
            .AddJwtAuthentication(builder.Configuration)
            .AddAuthorization();

        return builder;
    }

    /// <summary>
    /// Adds the authentication and authorization middleware, then maps the system, token
    /// and user endpoints. Authentication runs before the endpoints are reached.
    /// </summary>
    /// <param name="app">The built application.</param>
    /// <returns>The same application, for chaining.</returns>
    public static WebApplication UseAuthFacadeApi(this WebApplication app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapSystemEndpoints();
        app.MapTokenEndpoints();
        app.MapUserEndpoints();
        return app;
    }
}
