using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace OpenEmrNext.AuthFacade.Api.OpenIddictClient;

/// <summary>
/// Backs the <c>AuthFacadeSession</c> cookie with a server-side record in the same database ADR 0043
/// introduced (<see cref="AuthFacadeDbContext"/>), instead of baking the whole ticket into the cookie
/// itself (ADR 0044). The cookie then carries only an opaque key; the real ticket is fetched from here on
/// every request the cookie middleware authenticates. This is what makes a session something that can
/// actually be revoked: deleting the row behind a key, from anywhere, makes every copy of the cookie that
/// references it stop authenticating on its very next use — no cooperation needed from whichever browser
/// is holding it, which is exactly the "stolen cookie" scenario this exists to answer.
/// </summary>
/// <remarks>
/// Assigned once, as a singleton, to <see cref="CookieAuthenticationOptions.SessionStore"/>
/// (<see cref="OpenIddictClientServiceCollectionExtensions.AddOpenIddictClient"/>). Each method therefore
/// opens its own short-lived scope to resolve <see cref="AuthFacadeDbContext"/>, the same pattern a
/// singleton background service uses for a scoped dependency, rather than holding one scope for the
/// store's own lifetime.
/// </remarks>
internal sealed class AuthFacadeSessionStore(IServiceScopeFactory scopeFactory, IDataProtectionProvider dataProtectionProvider)
    : ITicketStore
{
    // A purpose string distinct from the cookie's own and from the Data Protection key ring's internal use,
    // so a future change to either can never accidentally let this protector unprotect something it was
    // not meant to, or vice versa.
    private readonly IDataProtector _protector =
        dataProtectionProvider.CreateProtector("OpenEmrNext.AuthFacade.SessionStore.v1");

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var key = Guid.NewGuid().ToString("N");
        await RenewAsync(key, ticket);
        return key;
    }

    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthFacadeDbContext>();

        var protectedTicket = _protector.Protect(TicketSerializer.Default.Serialize(ticket));
        var subject = ticket.Principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var expiresUtc = ticket.Properties.ExpiresUtc?.UtcDateTime;

        var record = await db.Sessions.FindAsync(key);
        if (record is null)
        {
            db.Sessions.Add(new AuthFacadeSessionRecord
            {
                Key = key,
                Subject = subject,
                ProtectedTicket = protectedTicket,
                ExpiresUtc = expiresUtc
            });
        }
        else
        {
            record.Subject = subject;
            record.ProtectedTicket = protectedTicket;
            record.ExpiresUtc = expiresUtc;
        }

        await db.SaveChangesAsync();
    }

    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthFacadeDbContext>();

        var record = await db.Sessions.FindAsync(key);
        if (record is null)
        {
            return null;
        }

        if (record.ExpiresUtc is { } expiresUtc && expiresUtc <= DateTime.UtcNow)
        {
            // Expired rows are cleaned up lazily, on the next attempt to read one, rather than by a
            // separate sweep: nothing else in this project runs background cleanup jobs today, and an
            // expired session is read at most once more before the cookie itself also expires client-side.
            db.Sessions.Remove(record);
            await db.SaveChangesAsync();
            return null;
        }

        return TicketSerializer.Default.Deserialize(_protector.Unprotect(record.ProtectedTicket));
    }

    public async Task RemoveAsync(string key)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthFacadeDbContext>();

        var record = await db.Sessions.FindAsync(key);
        if (record is not null)
        {
            db.Sessions.Remove(record);
            await db.SaveChangesAsync();
        }
    }
}
