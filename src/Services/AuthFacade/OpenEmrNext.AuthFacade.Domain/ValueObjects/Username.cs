using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace OpenEmrNext.AuthFacade.Domain.ValueObjects;

/// <summary>
/// A login name as submitted at login. Accepts any 1 to 255 characters without control
/// characters, matching OpenEMR's <c>users.username</c> column, so that existing accounts
/// are never rejected here. OpenEMR decides whether the name exists.
/// </summary>
public sealed partial record Username
{
    /// <summary>The login name, exactly as submitted.</summary>
    public string Value { get; }

    /// <summary>Creates a login name.</summary>
    /// <param name="value">The login name.</param>
    /// <exception cref="ArgumentException">The value is not 1 to 255 characters or contains a control character.</exception>
    public Username(string value)
    {
        if (!IsValid(value))
        {
            throw new ArgumentException(
                "Username must be 1 to 255 characters and contain no control characters.",
                nameof(value));
        }

        Value = value;
    }

    /// <summary>Parses a login name without throwing.</summary>
    /// <param name="value">The candidate login name. May be null.</param>
    /// <param name="username">The parsed login name when the method returns true; otherwise null.</param>
    /// <returns>True when the value passes the length and control-character rules.</returns>
    public static bool TryParse(string? value, [NotNullWhen(true)] out Username? username)
    {
        if (IsValid(value))
        {
            username = new Username(value);
            return true;
        }

        username = null;
        return false;
    }

    /// <summary>The single place that defines which login names are accepted.</summary>
    private static bool IsValid([NotNullWhen(true)] string? value) =>
        value is not null && UsernamePattern().IsMatch(value);

    [GeneratedRegex(@"\A[^\p{Cc}]{1,255}\z")]
    private static partial Regex UsernamePattern();
}
