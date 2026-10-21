using OpenEmrNext.AuthFacade.Api.Endpoints;
using OpenEmrNext.AuthFacade.Application;
using OpenEmrNext.AuthFacade.Infrastructure.OpenEmr;
using OpenEmrNext.AuthFacade.Infrastructure.Security;

namespace OpenEmrNext.AuthFacade.Api;

/// <summary>Wires the AuthFacade API. Called from Program and from in-process tests.</summary>
public static class AuthFacadeApiExtensions
{
    public static WebApplicationBuilder AddAuthFacadeApi(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddApplication()
            .AddOpenEmrIdentityProvider(builder.Configuration, builder.Environment)
            .AddServiceJwt(builder.Configuration);
        builder.Services.AddAuthorization();

        return builder;
    }

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
