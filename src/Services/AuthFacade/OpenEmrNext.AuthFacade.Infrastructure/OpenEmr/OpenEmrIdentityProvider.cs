using Microsoft.AspNetCore.Http;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using OpenEmrNext.AuthFacade.Application.Abstractions;
using OpenEmrNext.AuthFacade.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace OpenEmrNext.AuthFacade.Infrastructure.OpenEmr;

/// <summary>
/// Implements the identity provider by posting an OAuth password grant to OpenEMR's token endpoint.
/// </summary>
/// <remarks>
/// Credentials are never logged. The request is sent over the HTTP client configured in
/// <c>OpenEmrServiceCollectionExtensions</c>.
/// </remarks>
internal sealed class OpenEmrIdentityProvider(HttpClient http, IOptions<OpenEmrOptions> options)
    : IOpenEmrIdentityProvider
{
    private readonly OpenEmrOptions _options = options.Value;

    /// <inheritdoc/>
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

    /// <summary>Extracts the <c>sub</c> claim from the access token in OpenEMR's response body.</summary>
    /// <remarks>
    /// The token's signature is not verified. The subject is trusted because the token arrives
    /// over the TLS connection to OpenEMR, and this service only uses it to name the new token.
    /// </remarks>
    /// <param name="body">OpenEMR's successful response body.</param>
    /// <returns>The subject claim.</returns>
    /// <exception cref="IdentityProviderResponseException">The body has no usable access token or subject.</exception>
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
