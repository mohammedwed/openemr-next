namespace OpenEmrNext.PatientManagement.Application.Ports;

/// <summary>
/// Appends to Patient Management's own audit trail (SEC-9). A write's success row is appended <b>inside the write's own transaction</b>, as its last statement, so a change cannot commit without its audit row; a
/// refusal's row is appended on its own, because nothing was changed. If appending fails the caller gets an error: an action that cannot be audited is not answered as if it had been.
/// </summary>
public interface IAuditLog
{
    Task AppendAsync(AuditEntry entry, CancellationToken ct);
}
