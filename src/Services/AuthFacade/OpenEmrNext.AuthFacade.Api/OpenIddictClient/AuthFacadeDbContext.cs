using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace OpenEmrNext.AuthFacade.Api.OpenIddictClient;

/// <summary>
/// AuthFacade's own session-related state, and nothing else (ADR 0043): OpenIddict.Client's own state-token
/// store (CSRF/replay protection across the browser redirect, which OpenIddict.Core requires a real
/// database for) and the Data Protection key ring that protects the <c>AuthFacadeSession</c> cookie's
/// ticket (ADR 0027). No identity data, no application data — in the real Compose stack this points at a
/// dedicated MariaDB database, separate from OpenEMR's own, created and credentialed independently
/// (<c>scripts/seeds/authfacade-db.sh</c>); everywhere else (tests, a bare <c>dotnet run</c>) it falls back
/// to a throwaway per-process SQLite file, created with no migration history either way.
/// </summary>
internal sealed class AuthFacadeDbContext(DbContextOptions<AuthFacadeDbContext> options)
    : DbContext(options), IDataProtectionKeyContext
{
    /// <inheritdoc/>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.UseOpenIddict();

        if (Database.IsMySql())
        {
            // utf8mb4 (4 bytes/char, Pomelo's default) pushes OpenIddictTokens' own composite index
            // (ApplicationId + Status + Subject + Type) past MariaDB's 3072-byte max key length --
            // confirmed live: EnsureCreatedAsync crashed the app with MySqlException "Specified key was
            // too long". utf8mb3 (3 bytes/char) fits comfortably and loses nothing these columns ever
            // hold: client/application IDs, OAuth subjects and status/type labels, never user-facing
            // free text that would need a character outside the Basic Multilingual Plane.
            builder.HasCharSet(CharSet.Utf8Mb3, DelegationModes.ApplyToColumns);
        }
    }
}
