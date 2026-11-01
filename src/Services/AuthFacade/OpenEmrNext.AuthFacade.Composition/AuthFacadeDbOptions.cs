namespace OpenEmrNext.AuthFacade.Composition;

/// <summary>
/// Connection details for AuthFacade's own dedicated MariaDB database (ADR 0043) — separate from OpenEMR's
/// own, holding only AuthFacade's session-related state: OpenIddict.Client's state-token store (ADR 0026)
/// and the Data Protection key ring that protects the <c>AuthFacadeSession</c> cookie (ADR 0027). Bound
/// from the <c>AuthFacadeDb</c> section.
/// </summary>
/// <remarks>
/// Deliberately optional as a whole, unlike <see cref="OpenEmrOptions"/>: when <see cref="Host"/> is unset,
/// AuthFacade falls back to a throwaway per-process SQLite file instead — the same behavior this project
/// had before ADR 0043, still used by every unit test and a bare <c>dotnet run</c> outside Docker, neither
/// of which has a MariaDB instance to reach. Only the real Compose stack sets this section.
/// </remarks>
public sealed class AuthFacadeDbOptions
{
    /// <summary>The configuration section name, <c>AuthFacadeDb</c>.</summary>
    public const string SectionName = "AuthFacadeDb";

    /// <summary>The MariaDB server's hostname. Empty means "not configured" — see remarks.</summary>
    public string Host { get; set; } = "";

    /// <summary>The MariaDB server's port.</summary>
    public int Port { get; set; } = 3306;

    /// <summary>The dedicated database name, for example <c>authfacade</c>. Never OpenEMR's own.</summary>
    public string Database { get; set; } = "";

    /// <summary>The dedicated, least-privilege user that can reach only <see cref="Database"/>.</summary>
    public string User { get; set; } = "";

    /// <summary>The user's password.</summary>
    public string Password { get; set; } = "";

    /// <summary>Whether a MariaDB connection is actually configured. False means fall back to SQLite.</summary>
    public bool IsConfigured => !string.IsNullOrEmpty(Host) && !string.IsNullOrEmpty(Database) && !string.IsNullOrEmpty(User);
}
