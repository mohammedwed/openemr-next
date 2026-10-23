using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OpenEmrNext.AuthFacade.Application.Ports;
using OpenEmrNext.AuthFacade.Domain.ValueObjects;

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
    private const string TokenPath = "oauth2/default/token";
    private const int Status200OK = 200;

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

        var (statusCode, body) = await PostGrantAsync(form, ct);

        if (statusCode != Status200OK)
        {
            return new PasswordGrantResult(statusCode, body, null);
        }

        // Read the subject from OpenEMR's token (received over TLS).
        return new PasswordGrantResult(statusCode, body, ReadSubject(body));
    }

    /// <summary>Sends the form to OpenEMR's token endpoint and returns its status and body.</summary>
    /// <exception cref="IdentityProviderUnavailableException">OpenEMR could not be reached.</exception>
    private async Task<(int StatusCode, string Body)> PostGrantAsync(FormUrlEncodedContent form, CancellationToken ct)
    {
        try
        {
            using var response = await http.PostAsync(TokenPath, form, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            return ((int)response.StatusCode, body);
        }
        catch (HttpRequestException ex)
        {
            throw new IdentityProviderUnavailableException(ex);
        }
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
            var subject = new JwtSecurityTokenHandler().ReadJwtToken(ReadAccessToken(body)).Subject;
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

    /// <summary>Returns the <c>access_token</c> string from OpenEMR's response body.</summary>
    /// <exception cref="IdentityProviderResponseException">The body has no string <c>access_token</c>.</exception>
    private static string ReadAccessToken(string body)
    {
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("access_token", out var tokenElement) ||
            tokenElement.ValueKind != JsonValueKind.String)
        {
            throw new IdentityProviderResponseException("OpenEMR response has no access_token.");
        }

        return tokenElement.GetString()!;
    }
}
