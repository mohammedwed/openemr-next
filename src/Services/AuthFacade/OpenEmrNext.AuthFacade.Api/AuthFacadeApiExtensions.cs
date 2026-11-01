using OpenEmrNext.AuthFacade.Api.ForwardedHeaders;
using OpenEmrNext.AuthFacade.Api.OpenApi;
using OpenEmrNext.AuthFacade.Api.OpenIddictClient;
using OpenEmrNext.AuthFacade.Api.Proxy;
using OpenEmrNext.AuthFacade.Api.RateLimiting;
using OpenEmrNext.AuthFacade.Composition;

namespace OpenEmrNext.AuthFacade.Api;

/// <summary>Wires the AuthFacade API. Called from Program and from in-process tests.</summary>
public static class AuthFacadeApiExtensions
{
    /// <summary>
    /// Registers the shared composition root, then forwarded-headers trust, the OpenIddict client, BFF
    /// proxy, login rate limiting, authorization and OpenAPI document generation. Must run before the app
    /// is built.
    /// </summary>
    /// <param name="builder">The host builder to configure.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static WebApplicationBuilder AddAuthFacadeHttpDependencies(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddAuthFacade(builder.Configuration)
            .AddTrustedProxyForwardedHeaders()
            .AddOpenIddictClient(builder.Configuration)
            .AddOpenEmrBffProxy(builder.Configuration)
            .AddLoginRateLimiting()
            .AddAuthorization()
            .AddOpenApi(options => options.ConfigureAuthFacade());

        builder.Services.AddDataProtection();
        builder.Services.AddOptions<BrowserFlowOptions>()
            .Bind(builder.Configuration.GetSection(BrowserFlowOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return builder;
    }

    /// <summary>
    /// Adds the forwarded-headers, rate limiting, authentication and authorization middleware. Forwarded
    /// headers run first (ADR 0039): everything after it, including the rate limiter's own address keying
    /// (ADR 0038) and authentication's cookie-issuing, must see the request's real scheme and address, not
    /// the plain-HTTP hop and the gateway's own address between the browser and this container. Rate
    /// limiting runs before authentication, so an over-limit request to <c>/oidc/login</c> or
    /// <c>/callback</c> is refused before either endpoint's handler — or any call to OpenEMR — runs.
    /// Endpoints are mapped separately with <c>MapAuthFacadeEndpoints</c>.
    /// </summary>
    /// <param name="app">The built application.</param>
    /// <returns>The same application, for chaining.</returns>
    public static WebApplication UseAuthFacadeHttpMiddleware(this WebApplication app)
    {
        app.UseTrustedProxyForwardedHeaders();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }
}
