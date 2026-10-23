using System.Text.Json.Serialization;

namespace OpenEmrNext.AuthFacade.Api.Contracts.Responses;

/// <summary>An OAuth-style error body. <c>error_description</c> is omitted when null.</summary>
/// <param name="Error">A machine-readable error code, such as <c>invalid_request</c> or <c>server_error</c>.</param>
/// <param name="ErrorDescription">An optional human-readable description.</param>
public sealed record ErrorResponse(
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("error_description"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ErrorDescription = null);
