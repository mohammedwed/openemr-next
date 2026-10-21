using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenEmrNext.AuthFacade.Application.Abstractions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace OpenEmrNext.AuthFacade.Infrastructure.Security;

/// <summary>Registers token issuance and bearer-token validation for this service's JWTs.</summary>
public static class JwtServiceCollectionExtensions
{
    /// <summary>
    /// Binds and validates <see cref="JwtOptions"/> at startup, registers the access token
    /// issuer, and configures the JWT bearer scheme to accept only this service's tokens.
    /// </summary>
    /// <remarks>
    /// Validation checks the issuer, audience, signing key, and lifetime. Inbound claims are not
    /// mapped to legacy names, so <c>sub</c> stays as it is in the token.
    /// </remarks>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">The configuration that holds the <c>Jwt</c> section.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddServiceJwt(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();

        // Validates this service's own tokens (used by /me).
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Value.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Value.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Value.Key)),
                    ValidateLifetime = true
                };
            });

        return services;
    }
}
