using System.Diagnostics.CodeAnalysis;
using OpenEmrNext.AuthFacade.Domain.ValueObjects;

namespace OpenEmrNext.AuthFacade.Api.Contracts.Requests;

/// <summary>
/// The <c>grant_type</c> a password-grant request must carry. It exists so the token endpoint can say, in its
/// signature, that it accepts only the password grant: ASP.NET Core binds the form field with
/// <see cref="TryParse"/> and answers 400 for anything else. The value itself carries no information.
/// </summary>
public sealed record PasswordGrant
{
    private PasswordGrant()
    {
    }

    /// <summary>Parses a grant type, accepting only <c>password</c>.</summary>
    /// <param name="value">The submitted <c>grant_type</c>. May be null.</param>
    /// <param name="grant">The parsed grant when the method returns true; otherwise null.</param>
    /// <returns>True when the value is the password grant.</returns>
    public static bool TryParse(string? value, [NotNullWhen(true)] out PasswordGrant? grant)
    {
        var isPassword = GrantType.TryParse(value, out var grantType) && grantType.Value == GrantType.Password;
        grant = isPassword ? new PasswordGrant() : null;
        return isPassword;
    }
}
