namespace OpenEmrNext.AuthFacade.Api.Contracts.Requests;

/// <summary>Form field names accepted by <c>POST /connect/token</c>.</summary>
public static class TokenRequestFields
{
    /// <summary>The grant type: <c>password</c> or the RFC 8693 token exchange grant.</summary>
    public const string GrantType = "grant_type";

    /// <summary>The login name to verify.</summary>
    public const string Username = "username";

    /// <summary>The password to verify.</summary>
    public const string Password = "password";

    /// <summary>The OpenEMR-issued JWT to exchange, for the token exchange grant.</summary>
    public const string SubjectToken = "subject_token";

    /// <summary>The type of <c>subject_token</c>. Must be <c>urn:ietf:params:oauth:token-type:jwt</c>.</summary>
    public const string SubjectTokenType = "subject_token_type";
}
