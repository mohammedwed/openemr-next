using System.Text.Json.Serialization;

namespace OpenEmrNext.AuthFacade.Api.Contracts.Responses;

/// <summary>An OAuth-style error body. <c>error_description</c> is omitted when null.</summary>
/// <param name="Error">A machine-readable error code, such as <c>invalid_request</c> or <c>server_error</c>. Use the factory methods, so each code is spelled in one place.</param>
/// <param name="ErrorDescription">An optional human-readable description.</param>
public sealed record ErrorResponse(
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("error_description"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ErrorDescription = null)
{
    /// <summary>A required form field is missing or invalid, including an unsupported grant type.</summary>
    public static ErrorResponse InvalidRequest() => new("invalid_request");

    /// <summary>OpenEMR refused the username and password.</summary>
    public static ErrorResponse InvalidGrant() => new("invalid_grant", "The username or password is incorrect.");

    /// <summary>The client address is over the login rate limit.</summary>
    public static ErrorResponse TooManyRequests() => new("too_many_requests", "Too many login attempts. Try again later.");

    /// <summary>OpenEMR could not be reached or answered unexpectedly.</summary>
    /// <param name="description">What went wrong, in a sentence.</param>
    public static ErrorResponse ServerError(string description) => new("server_error", description);
}
