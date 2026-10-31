using Microsoft.EntityFrameworkCore;

namespace OpenEmrNext.AuthFacade.Api.OpenIddictClient;

/// <summary>
/// Holds nothing of AuthFacade's own: it exists only because OpenIddict.Client's interactive
/// authorization-code flow needs a store for its own state tokens (CSRF/replay protection across the
/// browser redirect), which OpenIddict.Core requires a real database for. A file-based SQLite database,
/// created at startup with no migration history, is enough for this; it holds no identity data.
/// </summary>
internal sealed class ClientDbContext(DbContextOptions<ClientDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.UseOpenIddict();
    }
}
