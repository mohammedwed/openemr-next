using OpenEmrNext.ClinicalCare.Application.Ports;

namespace OpenEmrNext.ClinicalCare.Application;

/// <param name="OfferedVersions">The <c>ETag</c>s the caller sent in <c>If-Match</c>.</param>
public sealed record DeleteIssueCommand(DeletableIssue Kind, Guid IssueUuid, IReadOnlyList<string> OfferedVersions, RequestOrigin? Origin = null);

/// <summary>
/// Deletes a problem or an allergy (M5n, ADR 0080). Thin by design (ARC-4): ask legacy's access control first (the same <c>admin/super</c> right legacy's <c>deleter.php</c> requires for an issue), find the issue
/// for the patient it belongs to, and hand the conditional removal to <see cref="IIssueDeleter"/> to commit with its event and audit row. The order of refusals is the same as for the form records: not allowed (403)
/// then not found (404) then changed since read (412); every refusal is audited as failed.
/// </summary>
public sealed class DeleteIssueUseCase(
    IAccessControl access,
    IProblemReader problems,
    IAllergyReader allergies,
    IIssueDeleter deleter,
    IAuditLog auditLog,
    TimeProvider clock)
{
    /// <exception cref="AccessDeniedException">The user does not hold the right legacy requires. Nothing is read or written; a <c>failed</c> audit row is.</exception>
    /// <exception cref="RecordNotFoundException">No such problem or allergy.</exception>
    /// <exception cref="RecordVersionConflictException">The issue is no longer the version the caller read. Nothing is written.</exception>
    public async Task ExecuteAsync(DeleteIssueCommand command, CancellationToken ct)
    {
        try
        {
            await DeleteAsync(command, ct);
        }
        catch (Exception ex) when (ex is AccessDeniedException or RecordNotFoundException or RecordVersionConflictException)
        {
            // A refused deletion changed nothing, so its audit row is its own statement (ADR 0069 #4); it fails closed like the others.
            await auditLog.AppendAsync(Audit(command, clock.GetUtcNow().UtcDateTime, AuditOutcome.Failed), ct);
            throw;
        }
    }

    private async Task DeleteAsync(DeleteIssueCommand command, CancellationToken ct)
    {
        if (!await access.IsAllowedAsync(command.Origin?.Actor?.Username, AccessRight.DeleteRecords, ct))
            throw new AccessDeniedException($"{AccessRight.DeleteRecords.Section}/{AccessRight.DeleteRecords.Value}");

        // The patient the issue belongs to, for the event. Read before the lock: the adapter re-finds the row itself and decides existence and version under the lock.
        var (patientUuid, patientPid) = command.Kind == DeletableIssue.Problem
            ? await problems.FindAsync(command.IssueUuid, ct) is { } p ? (p.PatientUuid, p.PatientPid) : throw new RecordNotFoundException(command.IssueUuid.ToString())
            : await allergies.FindAsync(command.IssueUuid, ct) is { } a ? (a.PatientUuid, a.PatientPid) : throw new RecordNotFoundException(command.IssueUuid.ToString());

        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var outcome = await deleter.DeleteAsync(
            new IssueDeletionRequest(
                command.Kind, command.IssueUuid, command.OfferedVersions,
                new IssueDeletedEvent(command.Kind, command.IssueUuid, patientUuid, patientPid, nowUtc),
                Audit(command, nowUtc, AuditOutcome.Success)),
            ct);

        switch (outcome.Status)
        {
            case DeletionStatus.Deleted:
                return;
            case DeletionStatus.Conflict:
                throw new RecordVersionConflictException(command.IssueUuid.ToString(), outcome.CurrentVersion);
            default:
                throw new RecordNotFoundException(command.IssueUuid.ToString());
        }
    }

    private static AuditEntry Audit(DeleteIssueCommand command, DateTime atUtc, AuditOutcome outcome) =>
        new(atUtc, Actors.Of(command.Origin), command.Kind == DeletableIssue.Problem ? AuditActions.ProblemDelete : AuditActions.AllergyDelete,
            command.IssueUuid, outcome, command.Origin?.CorrelationId, command.Origin?.SourceAddress);
}
