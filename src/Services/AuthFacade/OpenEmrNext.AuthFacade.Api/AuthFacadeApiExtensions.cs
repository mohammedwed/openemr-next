using OpenEmrNext.AuthFacade.Api.Authentication;
using OpenEmrNext.AuthFacade.Composition;

namespace OpenEmrNext.AuthFacade.Api;

/// <summary>Wires the AuthFacade API. Called from Program and from in-process tests.</summary>
public static class AuthFacadeApiExtensions
{
    /// <summary>
    /// Registers the shared composition root, then the HTTP-only bearer validation and authorization.
    /// Must run before the app is built.
    /// </summary>
    /// <param name="builder">The host builder to configure.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static WebApplicationBuilder AddAuthFacadeHttpDependencies(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddAuthFacade(builder.Configuration)
            .AddBearerAuthentication()
            .AddAuthorization();
        return builder;
    }

    /// <summary>
    /// Adds the authentication and authorization middleware. Endpoints are mapped separately
    /// with <c>MapAuthFacadeEndpoints</c>.
    /// </summary>
    /// <param name="app">The built application.</param>
    /// <returns>The same application, for chaining.</returns>
    public static WebApplication UseAuthFacadeHttpMiddleware(this WebApplication app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }
}
