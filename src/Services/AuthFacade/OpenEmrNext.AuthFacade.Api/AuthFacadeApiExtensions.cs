using OpenEmrNext.AuthFacade.Api.OpenApi;
using OpenEmrNext.AuthFacade.Api.OpenIddictClient;
using OpenEmrNext.AuthFacade.Api.Proxy;
using OpenEmrNext.AuthFacade.Composition;

namespace OpenEmrNext.AuthFacade.Api;

/// <summary>Wires the AuthFacade API. Called from Program and from in-process tests.</summary>
public static class AuthFacadeApiExtensions
{
    /// <summary>
    /// Registers the shared composition root, then the OpenIddict client, BFF proxy, authorization and
    /// OpenAPI document generation. Must run before the app is built.
    /// </summary>
    /// <param name="builder">The host builder to configure.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static WebApplicationBuilder AddAuthFacadeHttpDependencies(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddAuthFacade(builder.Configuration)
            .AddOpenIddictClient(builder.Configuration)
            .AddOpenEmrBffProxy(builder.Configuration)
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
