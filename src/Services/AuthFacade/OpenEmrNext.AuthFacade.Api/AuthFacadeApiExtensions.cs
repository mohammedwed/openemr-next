using OpenEmrNext.AuthFacade.Api.Authentication;
using OpenEmrNext.AuthFacade.Api.Endpoints;
using OpenEmrNext.AuthFacade.Api.OpenApi;
using OpenEmrNext.AuthFacade.Api.RateLimiting;
using OpenEmrNext.AuthFacade.Composition;
using OpenEmrNext.AuthFacade.Infrastructure.OpenEmr;

namespace OpenEmrNext.AuthFacade.Api;

/// <summary>Wires the AuthFacade API. Called from Program and from in-process tests.</summary>
public static class AuthFacadeApiExtensions
{
    /// <summary>
    /// Registers the shared composition root, then the HTTP-only bearer validation, login rate limiting,
    /// authorization and OpenAPI document generation. Must run before the app is built.
    /// </summary>
    /// <param name="builder">The host builder to configure.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static WebApplicationBuilder AddAuthFacadeHttpDependencies(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddAuthFacade(builder.Configuration)
            .AddBearerAuthentication()
            .AddLoginRateLimiting()
            .AddAuthorization()
            .AddOpenApi(options => options.ConfigureAuthFacade());

        // A form AuthFacade cannot bind returns a quiet 400 in every environment. ASP.NET Core's default in
        // Development is to throw instead, which puts the exception text in the response body.
        builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);

        // The API host knows its environment, so it refuses the development TLS bypass anywhere else.
        builder.Services.AddOptions<OpenEmrOptions>()
            .Validate(
                options => !options.AcceptAnyServerCertificate || builder.Environment.IsDevelopment(),
                "OpenEmr:AcceptAnyServerCertificate can be true only in the Development environment.")
            .ValidateOnStart();

        return builder;
    }

    /// <summary>
    /// Adds the rate limiting, authentication and authorization middleware. Rate limiting runs first,
    /// so over-limit requests are refused before any token is checked. Endpoints are mapped separately
    /// with <c>MapAuthFacadeEndpoints</c>.
    /// </summary>
    /// <param name="app">The built application.</param>
    /// <returns>The same application, for chaining.</returns>
    public static WebApplication UseAuthFacadeHttpMiddleware(this WebApplication app)
    {
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }
}
