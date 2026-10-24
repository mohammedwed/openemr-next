using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace OpenEmrNext.AuthFacade.Api.Authentication;

/// <summary>Registers the HTTP bearer scheme that accepts OpenEMR-issued tokens.</summary>
public static class BearerAuthenticationServiceCollectionExtensions
{
    /// <summary>The name of the bearer scheme, which <c>/api/v1/me</c> requires.</summary>
    public const string BearerScheme = "Bearer";

    /// <summary>
    /// Registers the bearer scheme. A bearer token is verified against OpenEMR's keys, so only tokens
    /// that OpenEMR issued for this client are accepted.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddBearerAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(BearerScheme)
            .AddScheme<AuthenticationSchemeOptions, OpenEmrBearerHandler>(BearerScheme, _ => { });

        return services;
    }
}
