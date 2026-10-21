using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OpenEmrNext.AuthFacade.Application.Abstractions;
using OpenEmrNext.AuthFacade.Infrastructure.Security;
using System.Text;

namespace OpenEmrNext.AuthFacade.UnitTests.Infrastructure;

public class JwtServiceCollectionExtensionsTests
{
    private const string Key = "unit-test-signing-key-at-least-32-bytes-long";

    private static IAccessTokenIssuer CreateIssuer()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Key"] = Key })
            .Build();

        var services = new ServiceCollection();
        services.AddServiceJwt(configuration);

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
