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
    public string Value { get; }

    public Username(string value)
    {
        if (!UsernamePattern().IsMatch(value))
        {
            throw new ArgumentException(
                "Username must be 1 to 255 characters and contain no control characters.",
                nameof(value));
        }

        Value = value;
    }

    public static bool TryParse(string? value, [NotNullWhen(true)] out Username? username)
    {
        if (value is not null && UsernamePattern().IsMatch(value))
        {
            username = new Username(value);
            return true;
        }

        username = null;
        return false;
    }

    [GeneratedRegex(@"\A[^\p{Cc}]{1,255}\z")]
    private static partial Regex UsernamePattern();
}
