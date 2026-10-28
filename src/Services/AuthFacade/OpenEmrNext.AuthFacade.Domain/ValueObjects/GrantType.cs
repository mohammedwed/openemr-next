using System.Text.RegularExpressions;

namespace OpenEmrNext.AuthFacade.Domain.ValueObjects;

/// <summary>
/// An OAuth 2.0 grant type this service supports. Add alternatives to the pattern
/// (e.g. <c>(password|refresh_token)</c>) as new grants are implemented.
/// </summary>
public sealed partial record GrantType
{
    /// <summary>The grant type string, exactly as submitted.</summary>
    public string Value { get; }

    /// <summary>Creates a grant type.</summary>
    /// <param name="value">The grant type string.</param>
    /// <exception cref="ArgumentException">The value is not a supported grant type.</exception>
    public GrantType(string value)
    {
        if (!IsSupported(value))
        {
            throw new ArgumentException("Unsupported grant type.", nameof(value));
        }

        Value = value;
    }

    /// <summary>The single place that defines which grant types are supported.</summary>
    private static bool IsSupported(string? value) =>
        value is not null && GrantTypePattern().IsMatch(value);

    /// <summary>The password grant, which verifies a username and password with OpenEMR.</summary>
    public const string Password = "password";

    [GeneratedRegex(@"\Apassword\z")]
    private static partial Regex GrantTypePattern();
}
