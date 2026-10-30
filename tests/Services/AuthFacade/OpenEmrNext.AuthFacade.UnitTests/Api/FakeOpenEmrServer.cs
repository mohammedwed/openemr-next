using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;

namespace OpenEmrNext.AuthFacade.UnitTests.Api;

/// <summary>
/// A real, minimal HTTP server standing in for OpenEMR's <c>/oauth2/default/token</c> and
/// <c>/oauth2/default/jwk</c> endpoints, for the one test (<c>OpenIddictClientCallbackTests</c>) that needs
/// an actual round trip through OpenIddict.Client's own <c>HttpClient</c> — unlike <c>/oidc/login</c>, which
/// makes no outbound call and needs no server at all. It does not implement <c>/authorize</c>: a test drives
/// <c>/callback</c> directly with a code and the <c>state</c> <c>/oidc/login</c> already produced, the same
/// way OpenEMR's own redirect would arrive, without actually simulating the browser's hop through it.
/// </summary>
internal sealed class FakeOpenEmrServer : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly RSA _rsa;

    private FakeOpenEmrServer(WebApplication app, RSA rsa)
    {
        _app = app;
        _rsa = rsa;
    }

    /// <summary>The server's own base URL, e.g. <c>http://127.0.0.1:54321</c>.</summary>
    public string BaseUrl => _app.Urls.First();

    /// <summary>The issuer this server's tokens carry — <c>{BaseUrl}/oauth2/default</c>, matching OpenEMR's shape.</summary>
    public string Issuer => $"{BaseUrl}/oauth2/default";

    /// <summary>The <c>aud</c> claim the next issued <c>id_token</c> carries. Must match the test's configured <c>OpenEmr:ClientId</c>.</summary>
    public string Audience { get; set; } = "facade-client";

    /// <summary>The <c>sub</c> claim the next issued <c>id_token</c> carries.</summary>
    public string Subject { get; set; } = "subject-1";

    /// <summary>The <c>nonce</c> claim the next issued <c>id_token</c> carries, or null to omit it.</summary>
    public string? Nonce { get; set; }

    /// <summary>When not 200, <c>/oauth2/default/token</c> returns this status with a generic OAuth error body instead of a token.</summary>
    public int TokenEndpointStatusCode { get; set; } = 200;

    /// <summary>Starts the server on a random loopback port.</summary>
    public static async Task<FakeOpenEmrServer> StartAsync()
    {
        var rsa = RSA.Create(2048);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        var server = new FakeOpenEmrServer(app, rsa);

        app.MapPost("/oauth2/default/token", () => server.TokenEndpointStatusCode == 200
            ? Results.Json(new
            {
                access_token = "fake-access-token",
                token_type = "Bearer",
                expires_in = 3600,
                id_token = server.BuildIdToken()
            })
            : Results.Json(new { error = "invalid_grant" }, statusCode: server.TokenEndpointStatusCode));

        app.MapGet("/oauth2/default/jwk", () =>
        {
            var key = rsa.ExportParameters(includePrivateParameters: false);
            return Results.Json(new
            {
                keys = new[]
                {
                    new
                    {
                        kty = "RSA",
                        n = Base64UrlEncoder.Encode(key.Modulus!),
                        e = Base64UrlEncoder.Encode(key.Exponent!)
                    }
                }
            });
        });

        await app.StartAsync();
        return server;
    }

    /// <summary>Signs an <c>id_token</c> with the server's own key, from the currently configured claims.</summary>
    private string BuildIdToken()
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Subject),
            // Required: OpenIddict rejects an id_token with no "iat" (ID2126). JwtSecurityToken's
            // constructor does not add one on its own unless passed explicitly.
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };
        if (Nonce is not null)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Nonce, Nonce));
        }

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-5),
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(new RsaSecurityKey(_rsa), SecurityAlgorithms.RsaSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async ValueTask DisposeAsync()
    {
        _rsa.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
