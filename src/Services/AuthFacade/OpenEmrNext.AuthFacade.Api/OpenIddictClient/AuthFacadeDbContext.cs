using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace OpenEmrNext.AuthFacade.Api.OpenIddictClient;

/// <summary>
/// AuthFacade's own session-related state, and nothing else (ADR 0043): OpenIddict.Client's own state-token
/// store (CSRF/replay protection across the browser redirect, which OpenIddict.Core requires a real
/// database for), the Data Protection key ring that protects the <c>AuthFacadeSession</c> cookie's ticket
/// (ADR 0027), and — since ADR 0044 — the tickets themselves, one row per session, so a session can
/// actually be revoked instead of only ever expiring on its own. No identity data, no application data — in
/// the real Compose stack this points at a dedicated MariaDB database, separate from OpenEMR's own, created
/// and credentialed independently (<c>scripts/seeds/authfacade-db.sh</c>); everywhere else (tests, a bare
/// <c>dotnet run</c>) it falls back to a throwaway per-process SQLite file, created with no migration
/// history either way.
/// </summary>
internal sealed class AuthFacadeDbContext(DbContextOptions<AuthFacadeDbContext> options)
    : DbContext(options), IDataProtectionKeyContext
{
    /// <inheritdoc/>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    /// <summary>One row per <c>AuthFacadeSession</c> cookie (ADR 0044). See <see cref="AuthFacadeSessionStore"/>.</summary>
    public DbSet<AuthFacadeSessionRecord> Sessions => Set<AuthFacadeSessionRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.UseOpenIddict();

        builder.Entity<AuthFacadeSessionRecord>(session =>
        {
            session.ToTable("AuthFacadeSessions");
            session.HasKey(s => s.Key);
            // Explicit lengths, not Pomelo's unbounded longtext default: MariaDB cannot index a
            // TEXT/BLOB-typed column without one, the same failure mode utf8mb3 below works around for
            // OpenIddict's own tables. 400 matches OpenIddictTokens' own Subject column (ADR 0043).
            session.Property(s => s.Key).HasMaxLength(64);
            session.Property(s => s.Subject).HasMaxLength(400);
            session.HasIndex(s => s.Subject);
        });

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
