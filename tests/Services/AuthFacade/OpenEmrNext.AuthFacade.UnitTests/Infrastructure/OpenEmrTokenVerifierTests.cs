using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenEmrNext.AuthFacade.Application.Ports;
using OpenEmrNext.AuthFacade.Infrastructure.OpenEmr;

namespace OpenEmrNext.AuthFacade.UnitTests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="OpenEmrTokenVerifier"/>. A throwaway RSA key signs the tokens, and a
/// fake JWKS endpoint publishes only its public half, as OpenEMR does. The tests check each rule
/// the verifier applies: algorithm, signature, issuer, audience and lifetime.
/// </summary>
public class OpenEmrTokenVerifierTests : IDisposable
{
    private const string BaseUrl = "https://openemr.test";
    private const string Issuer = "https://openemr.test/oauth2/default";
    private const string ClientId = "facade-client";

    private readonly RSA _rsa = RSA.Create(2048);
    private readonly FakeHttpMessageHandler _handler = new();

    public void Dispose() => _rsa.Dispose();

    /// <summary>Creates a verifier whose JWKS endpoint publishes the public half of the test key.</summary>
    private OpenEmrTokenVerifier CreateVerifier()
    {
        var publicParameters = _rsa.ExportParameters(includePrivateParameters: false);
        var jwk = $"{{\"kty\":\"RSA\",\"n\":\"{Base64UrlEncoder.Encode(publicParameters.Modulus!)}\",\"e\":\"{Base64UrlEncoder.Encode(publicParameters.Exponent!)}\"}}";

        _handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($"{{\"keys\":[{jwk}]}}", Encoding.UTF8, "application/json")
        };

        var http = new HttpClient(_handler) { BaseAddress = new Uri(BaseUrl + "/") };
        var options = Options.Create(new OpenEmrOptions
        {
            BaseUrl = BaseUrl,
            Issuer = Issuer,
            ClientId = ClientId,
            ClientSecret = "unused-in-verifier"
        });

        return new OpenEmrTokenVerifier(http, options);
    }

    /// <summary>Signs a token with the test key unless another key or algorithm is given.</summary>
    private string Sign(
        string issuer = Issuer,
        string audience = ClientId,
        DateTime? expires = null,
        string subject = "subject-1",
        string? name = null,
        SigningCredentials? credentials = null)
    {
        var expiresAt = expires ?? DateTime.UtcNow.AddMinutes(10);
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, subject) };
        if (name is not null)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Name, name));
        }

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: expiresAt.AddMinutes(-60),
            expires: expiresAt,
            signingCredentials: credentials ?? new SigningCredentials(new RsaSecurityKey(_rsa), SecurityAlgorithms.RsaSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public async Task Valid_token_returns_its_subject()
    {
        var verifier = CreateVerifier();

        var result = await verifier.VerifyAsync(Sign(subject: "subject-1"), CancellationToken.None);

        Assert.Equal("subject-1", result?.Subject);
    }

    [Fact]
    public async Task Name_claim_is_returned_when_the_token_has_one()
    {
        var verifier = CreateVerifier();

        var result = await verifier.VerifyAsync(Sign(name: "alice_01"), CancellationToken.None);

        Assert.Equal("alice_01", result?.Name);
    }

    [Fact]
    public async Task Name_is_null_when_the_token_has_no_name_claim()
    {
        var verifier = CreateVerifier();

        var result = await verifier.VerifyAsync(Sign(), CancellationToken.None);

        Assert.Null(result?.Name);
    }

    [Fact]
    public async Task Keys_are_fetched_from_the_jwks_endpoint_under_the_base_url()
    {
        var verifier = CreateVerifier();

        await verifier.VerifyAsync(Sign(), CancellationToken.None);

        Assert.Equal(new Uri("https://openemr.test/oauth2/default/jwk"), _handler.Request!.RequestUri);
    }

    [Fact]
    public async Task Token_for_another_audience_is_rejected()
    {
        var verifier = CreateVerifier();

        var result = await verifier.VerifyAsync(Sign(audience: "some-other-client"), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Token_from_another_issuer_is_rejected()
    {
        var verifier = CreateVerifier();

        var result = await verifier.VerifyAsync(Sign(issuer: "https://attacker.test/oauth2/default"), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Expired_token_is_rejected()
    {
        var verifier = CreateVerifier();

        var result = await verifier.VerifyAsync(Sign(expires: DateTime.UtcNow.AddMinutes(-30)), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Token_signed_with_another_key_is_rejected()
    {
        using var otherRsa = RSA.Create(2048);
        var otherKey = new RsaSecurityKey(otherRsa);
        var verifier = CreateVerifier();

        var result = await verifier.VerifyAsync(
            Sign(credentials: new SigningCredentials(otherKey, SecurityAlgorithms.RsaSha256)),
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Hmac_token_is_rejected_even_when_its_claims_are_valid()
    {
        // Guards against algorithm confusion: an HS256 token must not be checked with the RSA public key.
        var hmacKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("attacker-chosen-hmac-key-32-bytes-or-more"));
        var verifier = CreateVerifier();

        var result = await verifier.VerifyAsync(
            Sign(credentials: new SigningCredentials(hmacKey, SecurityAlgorithms.HmacSha256)),
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Malformed_token_is_rejected()
    {
        var verifier = CreateVerifier();

        var result = await verifier.VerifyAsync("not-a-jwt", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Unreachable_jwks_endpoint_is_reported_as_unavailable()
    {
        var verifier = CreateVerifier();
        _handler.Response = null;
        _handler.Exception = new HttpRequestException("connection refused");

        await Assert.ThrowsAsync<TokenVerifierUnavailableException>(
            () => verifier.VerifyAsync(Sign(), CancellationToken.None));
    }
}
