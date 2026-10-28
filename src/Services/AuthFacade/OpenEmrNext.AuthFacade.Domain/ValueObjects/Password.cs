using System.Threading;

namespace OpenEmrNext.AuthFacade.Domain.ValueObjects;

/// <summary>
/// A password as submitted at login. Only presence and a length guard are enforced, so
/// that every password OpenEMR accepts can be submitted; OpenEMR decides whether it is
/// correct. The plaintext can be retrieved exactly once through <see cref="TakeValue"/>;
/// after that the value object holds nothing.
/// </summary>
public sealed class Password
{
    /// <summary>Input guard against oversized requests. Not a legacy schema limit.</summary>
    public const int MaxLength = 1024;

    private char[]? _value;

    /// <summary>Creates a password and keeps a private copy of its characters.</summary>
    /// <param name="value">The plaintext password.</param>
    /// <exception cref="ArgumentException">The value is empty or longer than <see cref="MaxLength"/>.</exception>
    public Password(string value)
    {
        if (!IsValid(value))
        {
            throw new ArgumentException(
                $"Password must be 1 to {MaxLength} characters.",
                nameof(value));
        }

        _value = value.ToCharArray();
    }

    /// <summary>
    /// Returns the plaintext password and clears the internal copy. Can be called once only.
    /// </summary>
    /// <exception cref="InvalidOperationException">The password was already retrieved.</exception>
    public string TakeValue()
    {
        var chars = Interlocked.Exchange(ref _value, null)
            ?? throw new InvalidOperationException("The password has already been retrieved.");

        var value = new string(chars);
        Array.Clear(chars);
        return value;
    }

    /// <summary>Checks the presence and length rules. The content is not checked.</summary>
    private static bool IsValid(string? value) => value is not null && value.Length is >= 1 and <= MaxLength;

    /// <summary>
    /// Returns a fixed mask so the plaintext never appears in logs or string interpolation.
    /// </summary>
    public override string ToString() => "********";
}
