using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenEmrNext.AuthFacade.Application.Ports;

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
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        AddJwtOptions(services, configuration);
        AddSigningKey(services);
        AddAccessTokenIssuer(services);
        AddBearerAuthentication(services);

        return services;
    }

    /// <summary>Binds the <c>Jwt</c> section and validates it when the host starts.</summary>
    private static void AddJwtOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
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

    /// <summary>
    /// Configures the JWT bearer scheme, which validates this service's own tokens (used by <c>/me</c>).
    /// Only the issuer, audience, signing key, and lifetime are checked.
    /// </summary>
    private static void AddBearerAuthentication(IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, SymmetricSecurityKey>((bearer, jwtOptions, signingKey) =>
            {
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = CreateValidationParameters(jwtOptions.Value, signingKey);
            });
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
