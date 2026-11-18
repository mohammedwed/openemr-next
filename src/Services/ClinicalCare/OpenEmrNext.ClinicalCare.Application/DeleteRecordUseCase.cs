using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Application;

/// <param name="Key">The record's identifier: a uuid's text (vitals, clinical note) or the form id's digits (SOAP note).</param>
/// <param name="OfferedVersions">The <c>ETag</c>s the caller sent in <c>If-Match</c>.</param>
public sealed record DeleteRecordCommand(
    DeletableRecord Kind, Guid EncounterUuid, string Key, IReadOnlyList<string> OfferedVersions, RequestOrigin? Origin = null);

/// <summary>
/// Deletes vitals, a SOAP note or a clinical note (M5g, ADR 0076). Thin by design (ARC-4): ask legacy's access control first, find the encounter, and hand the conditional soft delete to
/// <see cref="IRecordDeleter"/> to commit with its event and audit row. The order of refusals is fixed: not signed in (401, before this) then not allowed (403) then no encounter or record (404) then changed since
/// read (412). The permission comes first so a user who may not delete learns nothing about which records exist. Every refusal is audited as failed.
/// </summary>
public sealed class DeleteRecordUseCase(
    IAccessControl access,
    IEncounterReader encounters,
    IRecordDeleter deleter,
    IAuditLog auditLog,
    TimeProvider clock)
{
    /// <exception cref="AccessDeniedException">The user does not hold the right legacy requires. Nothing is read or written; a <c>failed</c> audit row is.</exception>
    /// <exception cref="EncounterNotFoundException">No such encounter, or legacy deleted it.</exception>
    /// <exception cref="RecordNotFoundException">No such record in this encounter, or it is already deleted.</exception>
    /// <exception cref="RecordVersionConflictException">The record is no longer the version the caller read. Nothing is written.</exception>
    public async Task ExecuteAsync(DeleteRecordCommand command, CancellationToken ct)
    {
        try
        {
            await DeleteAsync(command, ct);
        }
        catch (Exception ex) when (ex is AccessDeniedException or EncounterNotFoundException or RecordNotFoundException or RecordVersionConflictException)
        {
            // A refused deletion changed nothing, so its audit row is its own statement (ADR 0069 #4); it fails closed like the others.
            await auditLog.AppendAsync(Audit(command, clock.GetUtcNow().UtcDateTime, AuditOutcome.Failed), ct);
            throw;
        }
    }

    private async Task DeleteAsync(DeleteRecordCommand command, CancellationToken ct)
    {
        // The user's own name is what legacy's rules are written against. A session with no username cannot be checked, so it is refused.
        if (!await access.IsAllowedAsync(command.Origin?.Actor?.Username, AccessRight.DeleteRecords, ct))
            throw new AccessDeniedException($"{AccessRight.DeleteRecords.Section}/{AccessRight.DeleteRecords.Value}");

        var view = await encounters.FindAsync(command.EncounterUuid, ct) ?? throw new EncounterNotFoundException(command.EncounterUuid);

        // An encounter with no number or no known patient owns no records, so there is nothing here to delete.
        var encounter = EncounterReference.TryCreate(view.Uuid, view.Number, view.PatientUuid, view.PatientPid)
            ?? throw new RecordNotFoundException(command.Key);

        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var outcome = await deleter.DeleteAsync(
            new RecordDeletionRequest(
                command.Kind, command.Key, encounter.Number, encounter.Patient.Pid, command.OfferedVersions,
                new RecordDeletedEvent(command.Kind, SubjectKey(command), encounter.Uuid, encounter.Number, encounter.Patient.Uuid, encounter.Patient.Pid, nowUtc),
                Audit(command, nowUtc, AuditOutcome.Success)),
            ct);

        switch (outcome.Status)
        {
            case DeletionStatus.Deleted:
                return;
            case DeletionStatus.Conflict:
                throw new RecordVersionConflictException(command.Key, outcome.CurrentVersion);
            default:
                throw new RecordNotFoundException(command.Key);
        }
    }

    private static string ActionOf(DeletableRecord kind) => kind switch
    {
        DeletableRecord.Vitals => AuditActions.VitalsDelete,
        DeletableRecord.SoapNote => AuditActions.SoapDelete,
        _ => AuditActions.ClinicalNoteDelete
    };

    /// <summary>A SOAP note has no uuid, so it is named by its form id (ADR 0073); the others by their uuid.</summary>
    private static string SubjectKey(DeleteRecordCommand command) => command.Kind == DeletableRecord.SoapNote ? $"soap:{command.Key}" : command.Key;

    private static AuditEntry Audit(DeleteRecordCommand command, DateTime atUtc, AuditOutcome outcome)
    {
        var actor = Actors.Of(command.Origin);
        var correlation = command.Origin?.CorrelationId;
        var address = command.Origin?.SourceAddress;

        // A uuid that is not one (a caller can only reach this through a typed route, but a refusal must never turn into a 500) falls back to a key.
        return command.Kind != DeletableRecord.SoapNote && Guid.TryParse(command.Key, out var uuid)
            ? new AuditEntry(atUtc, actor, ActionOf(command.Kind), uuid, outcome, correlation, address)
            : new AuditEntry(atUtc, actor, ActionOf(command.Kind), SubjectKey(command), outcome, correlation, address);
    }
}
