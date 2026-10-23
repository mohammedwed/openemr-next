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
    : IIdentityProvider
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

        return statusCode == Status200OK
            ? new PasswordGrantResult(statusCode, body, ReadAccessToken(body))
            : new PasswordGrantResult(statusCode, body);
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

    /// <summary>
    /// Reads the non-empty string <c>access_token</c> from OpenEMR's success body. The token is not
    /// verified here; the login handler verifies it before anything is returned.
    /// </summary>
    /// <param name="body">OpenEMR's successful response body.</param>
    /// <returns>The access token.</returns>
    /// <exception cref="IdentityProviderResponseException">The body is not JSON or has no usable <c>access_token</c>.</exception>
    private static string ReadAccessToken(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("access_token", out var token) &&
                token.ValueKind == JsonValueKind.String &&
                token.GetString() is { } value &&
                !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }
        catch (JsonException ex)
        {
            throw new IdentityProviderResponseException("OpenEMR returned an unreadable body.", ex);
        }

        throw new IdentityProviderResponseException("OpenEMR response has no access_token.");
    }
}
