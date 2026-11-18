using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenEmrNext.AuthFacade.Api.OpenIddictClient;

namespace OpenEmrNext.AuthFacade.UnitTests.Infrastructure;

/// <summary>
/// The session store's clean-up (M5j): an expired session is removed when a new one is created, so sessions that were abandoned and never presented again do not stay in the table for ever. Runs the real store against a
/// real (in-memory) SQLite database, which is the store the unit-test host and the dev stack use.
/// </summary>
public sealed class SessionStoreSweepTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly ServiceProvider _services;
    private readonly AuthFacadeSessionStore _store;

    public SessionStoreSweepTests()
    {
        _connection.Open();   // an in-memory database lives only as long as its connection
        var services = new ServiceCollection();
        services.AddDbContext<AuthFacadeDbContext>(o => o.UseSqlite(_connection));
        _services = services.BuildServiceProvider();
        using (var scope = _services.CreateScope())
            scope.ServiceProvider.GetRequiredService<AuthFacadeDbContext>().Database.EnsureCreated();

        _store = new AuthFacadeSessionStore(_services.GetRequiredService<IServiceScopeFactory>(), new EphemeralDataProtectionProvider());
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static AuthenticationTicket Ticket(DateTimeOffset? expires)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user-uuid")], "test"));
        return new AuthenticationTicket(principal, new AuthenticationProperties { ExpiresUtc = expires }, CookieAuthenticationDefaults.AuthenticationScheme);
    }

    private async Task SeedAsync(string key, DateTime? expiresUtc)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthFacadeDbContext>();
        db.Sessions.Add(new AuthFacadeSessionRecord { Key = key, Subject = "u", ProtectedTicket = [1, 2, 3], ExpiresUtc = expiresUtc });
        await db.SaveChangesAsync();
    }

    private async Task<List<string>> KeysAsync()
    {
        using var scope = _services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AuthFacadeDbContext>().Sessions.Select(s => s.Key).OrderBy(k => k).ToListAsync();
    }

    [Fact]
    public async Task A_new_sign_in_removes_sessions_that_expired_and_were_never_presented_again()
    {
        await SeedAsync("abandoned-1", DateTime.UtcNow.AddHours(-3));
        await SeedAsync("abandoned-2", DateTime.UtcNow.AddMinutes(-1));

        var key = await _store.StoreAsync(Ticket(DateTimeOffset.UtcNow.AddHours(12)));

        Assert.Equal([key], await KeysAsync());
    }

    [Fact]
    public async Task A_session_that_has_not_expired_is_never_swept()
    {
        await SeedAsync("live", DateTime.UtcNow.AddHours(2));
        await SeedAsync("no-expiry", null);

        var key = await _store.StoreAsync(Ticket(DateTimeOffset.UtcNow.AddHours(12)));

        Assert.Equal(new[] { key, "live", "no-expiry" }.Order(StringComparer.Ordinal), await KeysAsync());
    }

    [Fact]
    public async Task One_sign_in_sweeps_a_bounded_batch_and_a_backlog_is_worked_off_over_the_next_ones()
    {
        for (var i = 0; i < AuthFacadeSessionStore.SweepBatchSize + 50; i++)
            await SeedAsync($"old-{i:D3}", DateTime.UtcNow.AddHours(-5).AddSeconds(i));

        await _store.StoreAsync(Ticket(DateTimeOffset.UtcNow.AddHours(12)));
        Assert.Equal(50 + 1, (await KeysAsync()).Count);   // 100 swept, 50 old ones and the new one remain

        await _store.StoreAsync(Ticket(DateTimeOffset.UtcNow.AddHours(12)));
        Assert.Equal(2, (await KeysAsync()).Count);        // the rest swept; only the two new sessions remain
    }

    [Fact]
    public async Task Renewing_an_existing_session_keeps_it_and_does_not_need_a_sweep()
    {
        var key = await _store.StoreAsync(Ticket(DateTimeOffset.UtcNow.AddHours(12)));
        await SeedAsync("expired", DateTime.UtcNow.AddHours(-1));

        await _store.RenewAsync(key, Ticket(DateTimeOffset.UtcNow.AddHours(12)));

        Assert.Contains(key, await KeysAsync());
        Assert.NotNull(await _store.RetrieveAsync(key));
    }

    [Fact]
    public async Task An_expired_session_is_still_refused_and_removed_when_its_own_cookie_comes_back()
    {
        var key = await _store.StoreAsync(Ticket(DateTimeOffset.UtcNow.AddMinutes(-1)));

        Assert.Null(await _store.RetrieveAsync(key));
        Assert.DoesNotContain(key, await KeysAsync());
    }
}
