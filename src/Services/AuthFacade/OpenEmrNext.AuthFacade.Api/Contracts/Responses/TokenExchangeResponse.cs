using System.Text.Json.Serialization;

namespace OpenEmrNext.AuthFacade.Api.Contracts.Responses;

/// <summary>The body of a successful RFC 8693 token exchange.</summary>
/// <param name="AccessToken">The compact JWT issued by AuthFacade.</param>
/// <param name="IssuedTokenType">Always <c>urn:ietf:params:oauth:token-type:access_token</c>.</param>
/// <param name="TokenType">Always <c>Bearer</c>.</param>
/// <param name="ExpiresIn">Seconds until the token expires.</param>
public sealed record TokenExchangeResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("issued_token_type")] string IssuedTokenType,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);
