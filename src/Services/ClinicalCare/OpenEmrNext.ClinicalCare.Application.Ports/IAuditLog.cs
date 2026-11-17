namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>
/// The append-only audit trail (SEC-9, ADR 0069). There is deliberately no read, update or delete here: this service writes the
/// trail and never edits it, and its database user cannot either.
/// </summary>
public interface IAuditLog
{
    /// <summary>
    /// Appends <paramref name="entry"/>. When a registry's transaction is open the row joins it, so a change cannot commit
    /// without its audit row and an audit row cannot outlive a rolled-back change. With no transaction open it is its own
    /// statement (a refused request that changed nothing).
    /// </summary>
    Task AppendAsync(AuditEntry entry, CancellationToken ct);
}
