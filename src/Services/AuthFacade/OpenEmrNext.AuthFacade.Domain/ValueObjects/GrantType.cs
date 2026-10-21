using System.Diagnostics.CodeAnalysis;
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
        if (!GrantTypePattern().IsMatch(value))
        {
            throw new ArgumentException("Unsupported grant type.", nameof(value));
        }

        Value = value;
    }

    /// <summary>Parses a grant type without throwing.</summary>
    /// <param name="value">The candidate grant type string. May be null.</param>
    /// <param name="grantType">The parsed grant type when the method returns true; otherwise null.</param>
    /// <returns>True when the value is a supported grant type.</returns>
    public static bool TryParse(string? value, [NotNullWhen(true)] out GrantType? grantType)
    {
        if (value is not null && GrantTypePattern().IsMatch(value))
        {
            grantType = new GrantType(value);
            return true;
        }

        grantType = null;
        return false;
    }

    [GeneratedRegex(@"\A(password)\z")]
    private static partial Regex GrantTypePattern();
}
