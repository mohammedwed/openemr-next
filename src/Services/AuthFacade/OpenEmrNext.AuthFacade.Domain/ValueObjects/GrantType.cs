using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace OpenEmrNext.AuthFacade.Domain.ValueObjects;

/// <summary>
/// An OAuth 2.0 grant type this service supports. Add alternatives to the pattern
/// (e.g. <c>(password|refresh_token)</c>) as new grants are implemented.
/// </summary>
public sealed partial record GrantType
{
    public string Value { get; }

    public GrantType(string value)
    {
        if (!GrantTypePattern().IsMatch(value))
        {
            throw new ArgumentException("Unsupported grant type.", nameof(value));
        }

        Value = value;
    }

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
