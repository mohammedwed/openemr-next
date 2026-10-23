using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenEmrNext.AuthFacade.Infrastructure.Security;

namespace OpenEmrNext.AuthFacade.Api.Authentication;

/// <summary>Registers the HTTP bearer scheme that validates this service's own tokens.</summary>
public static class BearerAuthenticationServiceCollectionExtensions
{
    /// <summary>
    /// Configures the JWT bearer scheme, which validates this service's own tokens (used by <c>/me</c>).
    /// Only the issuer, audience, signing key, and lifetime are checked.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddBearerAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, SymmetricSecurityKey>((bearer, jwtOptions, signingKey) =>
            {
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = CreateValidationParameters(jwtOptions.Value, signingKey);
            });

        return services;
    }

    /// <summary>Builds the token checks that the bearer scheme applies to inbound tokens.</summary>
    private static TokenValidationParameters CreateValidationParameters(JwtOptions jwtOptions, SymmetricSecurityKey signingKey)
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true
        };
    }
}
