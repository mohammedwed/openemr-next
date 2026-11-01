namespace OpenEmrNext.AuthFacade.Api.OpenIddictClient;

/// <summary>
/// One server-side session record backing the <c>AuthFacadeSession</c> cookie (ADR 0044): the cookie itself
/// carries only <see cref="Key"/>; everything else — the real claims, and the real OpenEMR access/refresh
/// token inside them (ADR 0027) — lives here, protected the same way the cookie's own ticket would have
/// been. Deleting a row is what revocation actually is: the cookie that references it stops authenticating
/// on its very next request, from whichever process notices it is gone.
/// </summary>
internal sealed class AuthFacadeSessionRecord
{
    /// <summary>The opaque key the cookie carries in place of the full ticket.</summary>
    public required string Key { get; set; }

    /// <summary>
    /// The session's subject (the OpenEMR user this ticket was issued to), so every session belonging to
    /// one user can be found and revoked together without decrypting every row to check. Null for a
    /// session whose principal carries no subject claim.
    /// </summary>
    public string? Subject { get; set; }

    /// <summary>The serialized <see cref="Microsoft.AspNetCore.Authentication.AuthenticationTicket"/>, protected with Data Protection.</summary>
    public required byte[] ProtectedTicket { get; set; }

    /// <summary>When this session's ticket expires. Null for a ticket with no expiry of its own.</summary>
    public DateTime? ExpiresUtc { get; set; }
}
