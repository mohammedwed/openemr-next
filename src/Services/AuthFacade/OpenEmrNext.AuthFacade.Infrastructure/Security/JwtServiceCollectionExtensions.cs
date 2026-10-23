using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenEmrNext.AuthFacade.Application.Ports;

namespace OpenEmrNext.AuthFacade.Infrastructure.Security;

/// <summary>Registers this service's token issuance. Bearer validation is host-specific and lives in the API.</summary>
public static class JwtServiceCollectionExtensions
{
    /// <summary>
    /// Registers the signing key and the access token issuer. The key and issuer read
    /// <see cref="JwtOptions"/>, which the composition root binds and validates.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    internal static IServiceCollection AddJwtTokenIssuance(this IServiceCollection services)
    {
        AddSigningKey(services);
        AddAccessTokenIssuer(services);

        return services;
    }

    /// <summary>Registers the HMAC key, built once from <c>Jwt:Key</c> and shared by signing and validation.</summary>
    private static void AddSigningKey(IServiceCollection services)
    {
        services.AddSingleton(serviceProvider =>
        {
            var jwtOptions = serviceProvider.GetRequiredService<IOptions<JwtOptions>>().Value;
            return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key));
        });
    }

    /// <summary>Registers the issuer that creates this service's access tokens.</summary>
    private static void AddAccessTokenIssuer(IServiceCollection services)
    {
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
    }
}
