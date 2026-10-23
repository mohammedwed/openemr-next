using System.Text.Json.Serialization;

namespace OpenEmrNext.AuthFacade.Api.Contracts.Responses;

/// <summary>
/// The token response returned after a successful login. AuthFacade owns this shape, so it does not change
/// when OpenEMR's response does. <c>expires_in</c> and <c>refresh_token</c> are omitted when unknown.
/// </summary>
/// <param name="AccessToken">The OpenEMR access token, already verified.</param>
/// <param name="TokenType">The token type, normally <c>Bearer</c>.</param>
/// <param name="ExpiresIn">The lifetime in seconds, when known.</param>
/// <param name="RefreshToken">The refresh token, when OpenEMR issued one.</param>
public sealed record TokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? ExpiresIn,
    [property: JsonPropertyName("refresh_token"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? RefreshToken);
