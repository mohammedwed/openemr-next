namespace OpenEmrNext.AuthFacade.Api.Contracts.Requests;

/// <summary>Form field names accepted by <c>POST /connect/token</c>.</summary>
public static class TokenRequestFields
{
    /// <summary>The grant type. Only <c>password</c> is supported.</summary>
    public const string GrantType = "grant_type";

    /// <summary>The login name to verify.</summary>
    public const string Username = "username";

    /// <summary>The password to verify.</summary>
    public const string Password = "password";
}
