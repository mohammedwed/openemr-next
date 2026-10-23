using OpenEmrNext.AuthFacade.Api.Authentication;
using OpenEmrNext.AuthFacade.Composition;
using OpenEmrNext.AuthFacade.Infrastructure.OpenEmr;

namespace OpenEmrNext.AuthFacade.Api;

/// <summary>Wires the AuthFacade API. Called from Program and from in-process tests.</summary>
public static class AuthFacadeApiExtensions
{
    /// <summary>
    /// Registers the shared composition root, then the HTTP-only bearer validation, authorization and OpenAPI document generation.
    /// Must run before the app is built.
    /// </summary>
    /// <param name="builder">The host builder to configure.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static WebApplicationBuilder AddAuthFacadeHttpDependencies(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddAuthFacade(builder.Configuration)
            .AddBearerAuthentication()
            .AddAuthorization()
            .AddOpenApi();

        // The API host knows its environment, so it refuses the development TLS bypass anywhere else.
        builder.Services.AddOptions<OpenEmrOptions>()
            .Validate(
                options => !options.AcceptAnyServerCertificate || builder.Environment.IsDevelopment(),
                "OpenEmr:AcceptAnyServerCertificate can be true only in the Development environment.")
            .ValidateOnStart();

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
