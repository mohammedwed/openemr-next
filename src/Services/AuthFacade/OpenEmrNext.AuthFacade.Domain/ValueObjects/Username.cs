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

    /// <summary>The single place that defines which login names are accepted.</summary>
    private static bool IsValid(string? value) =>
        value is not null && UsernamePattern().IsMatch(value);

    [GeneratedRegex(@"\A[^\p{Cc}]{1,255}\z")]
    private static partial Regex UsernamePattern();
}
