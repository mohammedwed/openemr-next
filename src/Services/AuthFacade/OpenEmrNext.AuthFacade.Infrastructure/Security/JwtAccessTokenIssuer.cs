using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenEmrNext.AuthFacade.Application.Ports;

namespace OpenEmrNext.AuthFacade.Infrastructure.Security;

/// <summary>
/// Issues HMAC-SHA256 signed JWTs for this service. Tokens carry the <c>sub</c> and <c>name</c>
/// claims and are valid for <see cref="JwtOptions.LifetimeMinutes"/>.
/// </summary>
internal sealed class JwtAccessTokenIssuer(IOptions<JwtOptions> options, SymmetricSecurityKey key)
    : IAccessTokenIssuer
{
    private readonly JwtOptions _options = options.Value;
    private readonly SigningCredentials _credentials = new(key, SecurityAlgorithms.HmacSha256);

    /// <inheritdoc/>
    public IssuedAccessToken Issue(string subject, string name)
    {
        var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, subject), new Claim(JwtRegisteredClaimNames.Name, name) };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.LifetimeMinutes),
            signingCredentials: _credentials);

        return new IssuedAccessToken(
            new JwtSecurityTokenHandler().WriteToken(token),
            _options.LifetimeMinutes * 60);
    }
}
