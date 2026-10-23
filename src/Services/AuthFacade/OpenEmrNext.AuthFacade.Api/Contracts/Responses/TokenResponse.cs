using System.Text.Json.Serialization;

namespace OpenEmrNext.AuthFacade.Api.Contracts.Responses;

/// <summary>The body of a successful <c>POST /connect/token</c> response.</summary>
/// <param name="AccessToken">The compact JWT issued by AuthFacade.</param>
/// <param name="TokenType">Always <c>Bearer</c>.</param>
/// <param name="ExpiresIn">Seconds until the token expires.</param>
public sealed record TokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);
