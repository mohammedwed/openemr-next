using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace OpenEmrNext.AuthFacade.Api.BrowserFlow;

/// <summary>
/// The state one authorization-code flow (ADR 0024) carries between <c>/login</c> and <c>/callback</c>:
/// the CSRF <paramref name="State"/>, the <paramref name="Nonce"/> the <c>id_token</c> must echo, the PKCE
/// <paramref name="CodeVerifier"/>, and the <paramref name="ReturnUrl"/> already resolved against the
/// allow-list.
/// </summary>
internal sealed record OidcFlowState(string State, string Nonce, string CodeVerifier, string ReturnUrl);

/// <summary>
/// Protects and reads the <c>authfacade_oidc</c> cookie that carries <see cref="OidcFlowState"/>. The payload
/// is encrypted and tamper-proof via ASP.NET Core's Data Protection API, and it carries its own expiry, so a
/// stale value is refused even if the cookie's own <c>Max-Age</c> were ignored.
/// </summary>
internal sealed class OidcFlowCookieProtector
{
    /// <summary>The cookie name (ADR 0024).</summary>
    public const string CookieName = "authfacade_oidc";

    /// <summary>How long a flow has to complete, from <c>/login</c> to <c>/callback</c>.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private const string Purpose = "AuthFacade.OidcFlow.v1";

    private readonly ITimeLimitedDataProtector _protector;

    public OidcFlowCookieProtector(IDataProtectionProvider provider) =>
        _protector = provider.CreateProtector(Purpose).ToTimeLimitedDataProtector();

    /// <summary>Serializes and encrypts <paramref name="state"/> for the cookie value.</summary>
    public string Protect(OidcFlowState state) =>
        _protector.Protect(JsonSerializer.Serialize(state), Lifetime);

    /// <summary>
    /// Decrypts and deserializes a cookie value. Returns null for a missing, expired, tampered, or malformed
    /// value rather than throwing, since every caller treats all of those the same way: the flow cannot continue.
    /// </summary>
    public OidcFlowState? TryUnprotect(string? protectedValue)
    {
        if (string.IsNullOrEmpty(protectedValue))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<OidcFlowState>(_protector.Unprotect(protectedValue));
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
