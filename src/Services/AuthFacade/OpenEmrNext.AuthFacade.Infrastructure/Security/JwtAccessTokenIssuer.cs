using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using OpenEmrNext.AuthFacade.Application.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace OpenEmrNext.AuthFacade.Infrastructure.Security;

internal sealed class JwtAccessTokenIssuer(IOptions<JwtOptions> options) : IAccessTokenIssuer
{
    private readonly JwtOptions _options = options.Value;
    private readonly SigningCredentials _credentials = new(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Value.Key)),
        SecurityAlgorithms.HmacSha256);

    public IssuedAccessToken Issue(string subject, string name)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, subject),
            new Claim(JwtRegisteredClaimNames.Name, name)
        };

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
