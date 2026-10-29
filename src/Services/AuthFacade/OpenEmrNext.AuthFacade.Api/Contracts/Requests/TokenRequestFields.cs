namespace OpenEmrNext.AuthFacade.Api.Contracts.Requests;

/// <summary>Form field names accepted by <c>POST /api/v1/connect/token</c>.</summary>
public static class TokenRequestFields
{
    /// <summary>The grant type. Must be <c>password</c>.</summary>
    public const string GrantType = "grant_type";

    /// <summary>The login name to verify.</summary>
    public const string Username = "username";

    /// <summary>The password to verify.</summary>
    public const string Password = "password";

    /// <summary>The refresh token to redeem.</summary>
    public const string RefreshToken = "refresh_token";
}
