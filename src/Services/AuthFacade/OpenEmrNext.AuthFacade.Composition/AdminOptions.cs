namespace OpenEmrNext.AuthFacade.Composition;

/// <summary>
/// Which OpenEMR subjects may revoke another user's AuthFacade sessions (ADR 0045) — the admin-action half
/// of the gap ADR 0028/0040 both named, left open by <c>POST /sessions/revoke-all</c>'s self-service-only
/// scope (ADR 0044). Bound from the <c>Admin</c> section.
/// </summary>
/// <remarks>
/// Deliberately a config-driven allowlist, not a role or claim read from OpenEMR itself: AuthFacade has no
/// authorization model to validate such a claim against (the same gap <c>/api/v1/me</c> already has), and
/// a fixed list matches this project's conservative-local-stack precedent (ADR 0020). An empty list — the
/// default — means nobody can call the admin-revoke endpoint; it fails closed, not open.
/// </remarks>
public sealed class AdminOptions
{
    /// <summary>The configuration section name, <c>Admin</c>.</summary>
    public const string SectionName = "Admin";

    /// <summary>OpenEMR subjects (user IDs) trusted to revoke any session. Empty means nobody can.</summary>
    public List<string> Subjects { get; set; } = [];
}
