using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Application;

/// <summary>What an update did: the encounter as it now stands, and which fields changed (empty when nothing did).</summary>
public sealed record EncounterUpdateResult(EncounterView Encounter, IReadOnlyList<string> ChangedFields);

/// <summary>
/// Updates an encounter's editable fields (M5b phase 2, ADR 0068). Thin by design (ARC-4): read the record, check the caller's version, let the
/// domain validate, work out what changed, and hand the rest to <see cref="IEncounterUpdater"/> to commit with its event and audit row.
/// </summary>
public sealed class UpdateEncounterUseCase(
    IEncounterReader encounters,
    IEncounterCodeLists codeLists,
    IFacilityDirectory facilities,
    IEncounterUpdater updater,
    IAuditLog auditLog,
    TimeProvider clock)
{
    /// <exception cref="EncounterNotFoundException">No such encounter, or legacy deleted it. A <c>failed</c> audit row is written.</exception>
    /// <exception cref="ConcurrencyConflictException">The record is no longer the version the caller read. Nothing is written; a <c>failed</c> audit row is.</exception>
    /// <exception cref="ClinicalCareValidationException">One or more fields fail validation. Nothing is written; a <c>failed</c> audit row is.</exception>
    public async Task<EncounterUpdateResult> ExecuteAsync(UpdateEncounterCommand command, CancellationToken ct)
    {
        try
        {
            return await UpdateAsync(command, ct);
        }
        catch (Exception ex) when (ex is EncounterNotFoundException or ConcurrencyConflictException or ClinicalCareValidationException)
        {
            // A refused update changed nothing, so its audit row is its own statement (ADR 0069 #4); it fails closed like the creates.
            await auditLog.AppendAsync(
                new AuditEntry(
                    clock.GetUtcNow().UtcDateTime, Actors.Of(command.Origin), AuditActions.EncounterUpdate, command.EncounterUuid,
                    AuditOutcome.Failed, command.Origin?.CorrelationId, command.Origin?.SourceAddress),
                ct);
            throw;
        }
    }

    private async Task<EncounterUpdateResult> UpdateAsync(UpdateEncounterCommand command, CancellationToken ct)
    {
        var current = await encounters.FindAsync(command.EncounterUuid, ct)
            ?? throw new EncounterNotFoundException(command.EncounterUuid);

        // The precondition comes before the request is judged: an out-of-date caller is told so, whatever else is wrong with it. A row with no
        // stamp cannot be verified, so it cannot be updated.
        if (current.LastUpdate is not { } stamp || !command.Offered.Contains(ConcurrencyToken.From(stamp)))
            throw new ConcurrencyConflictException(command.EncounterUuid, current.LastUpdate);

        var classes = await codeLists.GetEncounterClassesAsync(ct);
        var facility = command.FacilityId > 0 ? await facilities.FindAsync(command.FacilityId, ct) : null;

        // Legacy datetime columns hold the server's local time (LEG-11); the event and the audit row carry UTC.
        var change = EncounterChange.Create(
            command.ClassCode, command.CalendarCategoryId, command.Date, command.OnsetDate, command.Reason,
            command.FacilityId, command.PosCode, command.ProviderId, clock.GetLocalNow().DateTime,
            new EncounterReferenceData(classes, facility));

        var changed = EncounterChangeSet.Between(current, change);
        if (changed.Count == 0)
            return new EncounterUpdateResult(current, changed);   // nothing to write, so no event and no audit row: nothing happened

        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var outcome = await updater.UpdateAsync(
            new EncounterUpdateRequest(
                command.EncounterUuid, ConcurrencyToken.From(stamp), change,
                new EncounterUpdatedEvent(
                    current.Uuid, current.Number, current.PatientUuid, current.PatientPid, changed, nowUtc),
                new AuditEntry(
                    nowUtc, Actors.Of(command.Origin), AuditActions.EncounterUpdate, command.EncounterUuid, AuditOutcome.Success,
                    command.Origin?.CorrelationId, command.Origin?.SourceAddress, changed)),
            ct);

        return outcome.Status switch
        {
            // The record is read again so the caller gets exactly what is stored, including its new stamp.
            UpdateStatus.Updated => new EncounterUpdateResult(
                await encounters.FindAsync(command.EncounterUuid, ct) ?? throw new EncounterNotFoundException(command.EncounterUuid), changed),
            UpdateStatus.Conflict => throw new ConcurrencyConflictException(command.EncounterUuid, outcome.Stamp),
            _ => throw new EncounterNotFoundException(command.EncounterUuid)
        };
    }
}
