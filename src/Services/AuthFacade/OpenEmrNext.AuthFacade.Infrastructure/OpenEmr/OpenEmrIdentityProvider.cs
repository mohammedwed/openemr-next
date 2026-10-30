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
    private const string DefaultTokenType = "Bearer";
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
            ? new PasswordGrantResult(statusCode, ReadToken(body))
            : new PasswordGrantResult(statusCode);
    }

    /// <inheritdoc/>
    public async Task<PasswordGrantResult> RefreshGrantAsync(RefreshToken refreshToken, CancellationToken ct)
    {
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["refresh_token"] = refreshToken.TakeValue(),
            ["scope"] = _options.Scope
        });

        var (statusCode, body) = await PostGrantAsync(form, ct);

        return statusCode == Status200OK
            ? new PasswordGrantResult(statusCode, ReadToken(body))
            : new PasswordGrantResult(statusCode);
    }

    /// <inheritdoc/>
    public async Task<PasswordGrantResult> AuthorizationCodeGrantAsync(
        string code, string codeVerifier, string redirectUri, CancellationToken ct)
    {
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = codeVerifier
        });

        var (statusCode, body) = await PostGrantAsync(form, ct);

        return statusCode == Status200OK
            ? new PasswordGrantResult(statusCode, ReadToken(body))
            : new PasswordGrantResult(statusCode);
    }

    /// <summary>Sends the form to OpenEMR's token endpoint and returns its status and body.</summary>
    /// <exception cref="IdentityProviderUnavailableException">OpenEMR could not be reached, or it timed out.</exception>
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
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // The client's own timeout fired. The caller did not cancel, so OpenEMR did not answer in time.
            throw new IdentityProviderUnavailableException(ex);
        }
    }

    /// <summary>
    /// Reads the token fields from OpenEMR's success body. The access token is not verified here;
    /// the login handler verifies it before anything is returned.
    /// </summary>
    /// <param name="body">OpenEMR's successful response body.</param>
    /// <returns>The token fields. <c>token_type</c> defaults to <c>Bearer</c> when OpenEMR omits it.</returns>
    /// <exception cref="IdentityProviderResponseException">The body is not JSON or has no usable <c>access_token</c>.</exception>
    private static OpenEmrToken ReadToken(string body)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(body);
        }
        catch (JsonException ex)
        {
            throw new IdentityProviderResponseException("OpenEMR returned an unreadable body.", ex);
        }

        using (doc)
        {
            var root = doc.RootElement;

            if (!root.TryGetProperty("access_token", out var accessElement) ||
                accessElement.ValueKind != JsonValueKind.String ||
                accessElement.GetString() is not { } accessToken ||
                string.IsNullOrWhiteSpace(accessToken))
            {
                throw new IdentityProviderResponseException("OpenEMR response has no access_token.");
            }

            var tokenType = root.TryGetProperty("token_type", out var typeElement) &&
                            typeElement.ValueKind == JsonValueKind.String &&
                            !string.IsNullOrWhiteSpace(typeElement.GetString())
                ? typeElement.GetString()!
                : DefaultTokenType;

            int? expiresIn = root.TryGetProperty("expires_in", out var expiryElement) &&
                             expiryElement.ValueKind == JsonValueKind.Number &&
                             expiryElement.TryGetInt32(out var seconds)
                ? seconds
                : null;

            var refreshToken = root.TryGetProperty("refresh_token", out var refreshElement) &&
                               refreshElement.ValueKind == JsonValueKind.String &&
                               !string.IsNullOrWhiteSpace(refreshElement.GetString())
                ? refreshElement.GetString()
                : null;

            var idToken = root.TryGetProperty("id_token", out var idTokenElement) &&
                          idTokenElement.ValueKind == JsonValueKind.String &&
                          !string.IsNullOrWhiteSpace(idTokenElement.GetString())
                ? idTokenElement.GetString()
                : null;

            return new OpenEmrToken(accessToken, tokenType, expiresIn, refreshToken, idToken);
        }
    }
}
