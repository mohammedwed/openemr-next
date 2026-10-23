using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OpenEmrNext.AuthFacade.Application.Ports;
using OpenEmrNext.AuthFacade.Infrastructure.Security;

namespace OpenEmrNext.AuthFacade.UnitTests.Infrastructure;

/// <summary>
/// Unit tests for <c>AddJwtAuthentication</c>. They check the claims, issuer, audience, signature,
/// and lifetime of tokens that the registered <see cref="IAccessTokenIssuer"/> creates.
/// </summary>
public class JwtServiceCollectionExtensionsTests
{
    /// <summary>The signing key used by these tests. Long enough for HMAC-SHA256.</summary>
    private const string Key = "unit-test-signing-key-at-least-32-bytes-long";

    /// <summary>Registers the JWT services from configuration and resolves the issuer.</summary>
    private static IAccessTokenIssuer CreateIssuer()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = Key,
                ["Jwt:Issuer"] = "openemr-next",
                ["Jwt:Audience"] = "openemr-next-api",
                ["Jwt:LifetimeMinutes"] = "30"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddJwtAuthentication(configuration);

        return services.BuildServiceProvider().GetRequiredService<IAccessTokenIssuer>();
    }

    [Fact]
    public void Issued_token_carries_subject_name_issuer_and_audience()
    {
        var issued = CreateIssuer().Issue("subject-123", "alice_01");

        var token = new JwtSecurityTokenHandler().ReadJwtToken(issued.AccessToken);

        Assert.Equal("subject-123", token.Subject);
        Assert.Equal("alice_01", token.Claims.Single(c => c.Type == "unique_name" || c.Type == "name").Value);
        Assert.Equal("openemr-next", token.Issuer);
        Assert.Contains("openemr-next-api", token.Audiences);
    }

    [Fact]
    public void Issued_token_is_signed_with_the_configured_key()
    {
        var issued = CreateIssuer().Issue("subject-123", "alice_01");

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "openemr-next",
            ValidateAudience = true,
            ValidAudience = "openemr-next-api",
            ValidateLifetime = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key))
        };

        var principal = new JwtSecurityTokenHandler().ValidateToken(issued.AccessToken, parameters, out _);

        Assert.True(principal.Identity?.IsAuthenticated);
    }

    [Fact]
    public void Issued_token_expires_after_the_reported_lifetime()
    {
        var issued = CreateIssuer().Issue("subject-123", "alice_01");

        var token = new JwtSecurityTokenHandler().ReadJwtToken(issued.AccessToken);
        var remaining = token.ValidTo - DateTime.UtcNow;

        Assert.Equal(1800, issued.ExpiresInSeconds);
        Assert.InRange(remaining.TotalSeconds, 1700, 1800);
    }
}
