namespace OpenEmrNext.AuthFacade.Domain.ValueObjects;

/// <summary>
/// A refresh token as submitted to the refresh grant. Only presence and a length guard are enforced, so
/// that every refresh token OpenEMR issued can be submitted back; OpenEMR decides whether it is still valid.
/// The plaintext can be retrieved exactly once through <see cref="TakeValue"/>; after that the value object
/// holds nothing, the same guard <see cref="Password"/> keeps (see ADR 0009): a refresh token is as much a
/// bearer secret as a password, since holding it is enough to mint a new access token.
/// </summary>
public sealed class RefreshToken
{
    /// <summary>Input guard against oversized requests. Not a legacy schema limit.</summary>
    public const int MaxLength = 4096;

    private char[]? _value;

    /// <summary>Creates a refresh token and keeps a private copy of its characters.</summary>
    /// <param name="value">The refresh token, exactly as OpenEMR issued it.</param>
    /// <exception cref="ArgumentException">The value is empty or longer than <see cref="MaxLength"/>.</exception>
    public RefreshToken(string value)
    {
        if (!IsValid(value))
        {
            throw new ArgumentException(
                $"Refresh token must be 1 to {MaxLength} characters.",
                nameof(value));
        }

        _value = value.ToCharArray();
    }

    /// <summary>
    /// Returns the refresh token and clears the internal copy. Can be called once only.
    /// </summary>
    /// <exception cref="InvalidOperationException">The refresh token was already retrieved.</exception>
    public string TakeValue()
    {
        var chars = Interlocked.Exchange(ref _value, null)
            ?? throw new InvalidOperationException("The refresh token has already been retrieved.");

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
