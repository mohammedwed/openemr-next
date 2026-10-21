using Microsoft.AspNetCore.Http;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using OpenEmrNext.AuthFacade.Application.Abstractions;
using OpenEmrNext.AuthFacade.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace OpenEmrNext.AuthFacade.Infrastructure.OpenEmr;

internal sealed class OpenEmrIdentityProvider(HttpClient http, IOptions<OpenEmrOptions> options)
    : IOpenEmrIdentityProvider
{
    private readonly OpenEmrOptions _options = options.Value;

    // Credentials are never logged.
    public async Task<PasswordGrantResult> PasswordGrantAsync(
        Username username, Password password, CancellationToken ct)
    {
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["user_role"] = "users",
            ["username"] = username.Value,
            ["password"] = password.TakeValue(),
            ["scope"] = _options.Scope
        });

        string body;
        int statusCode;
        try
        {
            using var response = await http.PostAsync("oauth2/default/token", form, ct);
            body = await response.Content.ReadAsStringAsync(ct);
            statusCode = (int)response.StatusCode;
        }
        catch (HttpRequestException ex)
        {
            throw new IdentityProviderUnavailableException(ex);
        }

        if (statusCode != StatusCodes.Status200OK)
        {
            return new PasswordGrantResult(statusCode, body, null);
        }

        // Read the subject from OpenEMR's token (received over TLS).
        return new PasswordGrantResult(statusCode, body, ReadSubject(body));
    }

    private static string ReadSubject(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("access_token", out var tokenElement) ||
                tokenElement.ValueKind != JsonValueKind.String)
            {
                throw new IdentityProviderResponseException("OpenEMR response has no access_token.");
            }

            var subject = new JwtSecurityTokenHandler().ReadJwtToken(tokenElement.GetString()!).Subject;
            if (string.IsNullOrWhiteSpace(subject))
            {
                throw new IdentityProviderResponseException("OpenEMR access_token has no subject.");
            }

            return subject;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException)
        {
            throw new IdentityProviderResponseException("OpenEMR returned an unreadable token.", ex);
        }
    }
}
