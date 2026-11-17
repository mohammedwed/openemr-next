using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.Problems;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Application;

/// <summary>
/// Everything a caller supplies to update a problem: which one, the version the caller read, and the whole editable set. Raw facts: <c>ProblemChange.Create</c>
/// owns the rules. To resolve a problem, send an end date (and usually outcome 1).
/// </summary>
/// <param name="Offered">The tags the caller sent in <c>If-Match</c>; the update applies only if one is the current version (LEG-10).</param>
public sealed record UpdateProblemCommand(
    Guid ProblemUuid,
    IReadOnlyList<ConcurrencyToken> Offered,
    string? Title,
    DateTime? BeginDate,
    DateTime? EndDate,
    string? Diagnosis,
    int Outcome,
    int Occurrence,
    string? ReferredBy,
    string? Comments,
    string? Verification,
    RequestOrigin? Origin = null);

/// <summary>What an update did: the problem as it now stands, and which fields changed (empty when nothing did).</summary>
public sealed record ProblemUpdateResult(ProblemView Problem, IReadOnlyList<string> ChangedFields);

/// <summary>
/// Updates a problem's editable fields (M5c slice 3, ADR 0068). Thin by design (ARC-4): read the record, check the caller's version, let the domain validate,
/// work out what changed, and hand the rest to <see cref="IProblemUpdater"/> to commit with its event and audit row. A refused update is audited as failed.
/// </summary>
public sealed class UpdateProblemUseCase(
    IProblemReader problems,
    IIssueCodeLists codeLists,
    IProblemUpdater updater,
    IAuditLog auditLog,
    TimeProvider clock)
{
    /// <exception cref="ProblemNotFoundException">No such problem (or the uuid is another issue type's). A <c>failed</c> audit row is written.</exception>
    /// <exception cref="ConcurrencyConflictException">The record is no longer the version the caller read. Nothing is written; a <c>failed</c> audit row is.</exception>
    /// <exception cref="ClinicalCareValidationException">One or more fields fail validation. Nothing is written; a <c>failed</c> audit row is.</exception>
    public async Task<ProblemUpdateResult> ExecuteAsync(UpdateProblemCommand command, CancellationToken ct)
    {
        try
        {
            return await UpdateAsync(command, ct);
        }
        catch (Exception ex) when (ex is ProblemNotFoundException or ConcurrencyConflictException or ClinicalCareValidationException)
        {
            // A refused update changed nothing, so its audit row is its own statement (ADR 0069 #4); it fails closed like the creates.
            await auditLog.AppendAsync(
                new AuditEntry(
                    clock.GetUtcNow().UtcDateTime, AuditActor.Unknown, AuditActions.ProblemUpdate, command.ProblemUuid,
                    AuditOutcome.Failed, command.Origin?.CorrelationId, command.Origin?.SourceAddress),
                ct);
            throw;
        }
    }

    private async Task<ProblemUpdateResult> UpdateAsync(UpdateProblemCommand command, CancellationToken ct)
    {
        var current = await problems.FindAsync(command.ProblemUuid, ct)
            ?? throw new ProblemNotFoundException(command.ProblemUuid);

        // The precondition comes before the request is judged: an out-of-date caller is told so, whatever else is wrong with it. A row with no stamp cannot be
        // verified, so it cannot be updated.
        if (current.LastModified is not { } stamp || !command.Offered.Contains(ConcurrencyToken.From(stamp)))
            throw new ConcurrencyConflictException(command.ProblemUuid, current.LastModified);

        var codes = await codeLists.GetCodesAsync(ct);

        // Legacy datetime columns hold the server's local time (LEG-11); the event and the audit row carry UTC.
        var change = ProblemChange.Create(
            command.Title, command.BeginDate, command.EndDate, command.Diagnosis, command.Outcome, command.Occurrence, command.ReferredBy, command.Comments,
            command.Verification, clock.GetLocalNow().DateTime, codes);

        var changed = ProblemChangeSet.Between(current, change);
        if (changed.Count == 0)
            return new ProblemUpdateResult(current, changed);   // nothing to write, so no event and no audit row: nothing happened

        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var outcome = await updater.UpdateAsync(
            new ProblemUpdateRequest(
                command.ProblemUuid, ConcurrencyToken.From(stamp), change,
                new ProblemUpdatedEvent(current.Uuid, current.PatientUuid, current.PatientPid, changed, nowUtc),
                new AuditEntry(
                    nowUtc, AuditActor.Unknown, AuditActions.ProblemUpdate, command.ProblemUuid, AuditOutcome.Success,
                    command.Origin?.CorrelationId, command.Origin?.SourceAddress, changed)),
            ct);

        return outcome.Status switch
        {
            // The record is read again so the caller gets exactly what is stored, including its new stamp.
            UpdateStatus.Updated => new ProblemUpdateResult(
                await problems.FindAsync(command.ProblemUuid, ct) ?? throw new ProblemNotFoundException(command.ProblemUuid), changed),
            UpdateStatus.Conflict => throw new ConcurrencyConflictException(command.ProblemUuid, outcome.Stamp),
            _ => throw new ProblemNotFoundException(command.ProblemUuid)
        };
    }
}
