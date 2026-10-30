using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using OpenEmrNext.AuthFacade.Application.Ports;

namespace OpenEmrNext.AuthFacade.Api.BrowserFlow;

/// <summary>
/// Protects and reads the <c>authfacade_session</c> cookie that carries OpenEMR's token response after a
/// successful browser login (ADR 0024). AuthFacade issues no token of its own (ADR 0002, ADR 0013); this
/// cookie is a transport for OpenEMR's, not a new token format.
/// </summary>
internal sealed class SessionCookieProtector
{
    /// <summary>The cookie name (ADR 0024).</summary>
    public const string CookieName = "authfacade_session";

    /// <summary>The session's outer cap, applied to both the cookie's <c>Max-Age</c> and the payload's own expiry.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(12);

    private const string Purpose = "AuthFacade.Session.v1";

    private readonly ITimeLimitedDataProtector _protector;

    public SessionCookieProtector(IDataProtectionProvider provider) =>
        _protector = provider.CreateProtector(Purpose).ToTimeLimitedDataProtector();

    /// <summary>Serializes and encrypts OpenEMR's token response for the cookie value.</summary>
    public string Protect(OpenEmrToken token) =>
        _protector.Protect(JsonSerializer.Serialize(token), Lifetime);

    /// <summary>
    /// Decrypts and deserializes a cookie value. Returns null for a missing, expired, tampered, or malformed
    /// value, the same as <see cref="OidcFlowCookieProtector.TryUnprotect"/>.
    /// </summary>
    public OpenEmrToken? TryUnprotect(string? protectedValue)
    {
        if (string.IsNullOrEmpty(protectedValue))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<OpenEmrToken>(_protector.Unprotect(protectedValue));
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
