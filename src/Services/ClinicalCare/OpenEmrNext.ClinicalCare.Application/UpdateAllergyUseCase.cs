using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.Allergies;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Application;

/// <summary>
/// Everything a caller supplies to update an allergy: which one, the version the caller read, and the whole editable set. Raw facts: <c>AllergyChange.Create</c>
/// owns the rules. To end an allergy, send an end date.
/// </summary>
/// <param name="Offered">The tags the caller sent in <c>If-Match</c>; the update applies only if one is the current version (LEG-10).</param>
public sealed record UpdateAllergyCommand(
    Guid AllergyUuid,
    IReadOnlyList<ConcurrencyToken> Offered,
    string? Title,
    DateTime? BeginDate,
    DateTime? EndDate,
    string? Diagnosis,
    string? Reaction,
    string? Severity,
    string? Verification,
    string? Comments,
    RequestOrigin? Origin = null);

/// <summary>What an update did: the allergy as it now stands, and which fields changed (empty when nothing did).</summary>
public sealed record AllergyUpdateResult(AllergyView Allergy, IReadOnlyList<string> ChangedFields);

/// <summary>
/// Updates an allergy's editable fields (M5c slice 3, ADR 0068). The same shape and the same order of checks as <see cref="UpdateProblemUseCase"/>: read, check the
/// caller's version, validate, diff, write only a real change. A refused update is audited as failed.
/// </summary>
public sealed class UpdateAllergyUseCase(
    IAllergyReader allergies,
    IAllergyCodeLists codeLists,
    IAllergyUpdater updater,
    IAuditLog auditLog,
    TimeProvider clock)
{
    /// <exception cref="AllergyNotFoundException">No such allergy (or the uuid is another issue type's). A <c>failed</c> audit row is written.</exception>
    /// <exception cref="ConcurrencyConflictException">The record is no longer the version the caller read. Nothing is written; a <c>failed</c> audit row is.</exception>
    /// <exception cref="ClinicalCareValidationException">One or more fields fail validation. Nothing is written; a <c>failed</c> audit row is.</exception>
    public async Task<AllergyUpdateResult> ExecuteAsync(UpdateAllergyCommand command, CancellationToken ct)
    {
        try
        {
            return await UpdateAsync(command, ct);
        }
        catch (Exception ex) when (ex is AllergyNotFoundException or ConcurrencyConflictException or ClinicalCareValidationException)
        {
            await auditLog.AppendAsync(
                new AuditEntry(
                    clock.GetUtcNow().UtcDateTime, Actors.Of(command.Origin), AuditActions.AllergyUpdate, command.AllergyUuid,
                    AuditOutcome.Failed, command.Origin?.CorrelationId, command.Origin?.SourceAddress),
                ct);
            throw;
        }
    }

    private async Task<AllergyUpdateResult> UpdateAsync(UpdateAllergyCommand command, CancellationToken ct)
    {
        var current = await allergies.FindAsync(command.AllergyUuid, ct)
            ?? throw new AllergyNotFoundException(command.AllergyUuid);

        if (current.LastModified is not { } stamp || !command.Offered.Contains(ConcurrencyToken.From(stamp)))
            throw new ConcurrencyConflictException(command.AllergyUuid, current.LastModified);

        var codes = await codeLists.GetCodesAsync(ct);

        var change = AllergyChange.Create(
            command.Title, command.BeginDate, command.EndDate, command.Diagnosis, command.Reaction, command.Severity, command.Verification, command.Comments,
            clock.GetLocalNow().DateTime, codes);

        var changed = AllergyChangeSet.Between(current, change);
        if (changed.Count == 0)
            return new AllergyUpdateResult(current, changed);

        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var outcome = await updater.UpdateAsync(
            new AllergyUpdateRequest(
                command.AllergyUuid, ConcurrencyToken.From(stamp), change,
                new AllergyUpdatedEvent(current.Uuid, current.PatientUuid, current.PatientPid, changed, nowUtc),
                new AuditEntry(
                    nowUtc, Actors.Of(command.Origin), AuditActions.AllergyUpdate, command.AllergyUuid, AuditOutcome.Success,
                    command.Origin?.CorrelationId, command.Origin?.SourceAddress, changed)),
            ct);

        return outcome.Status switch
        {
            UpdateStatus.Updated => new AllergyUpdateResult(
                await allergies.FindAsync(command.AllergyUuid, ct) ?? throw new AllergyNotFoundException(command.AllergyUuid), changed),
            UpdateStatus.Conflict => throw new ConcurrencyConflictException(command.AllergyUuid, outcome.Stamp),
            _ => throw new AllergyNotFoundException(command.AllergyUuid)
        };
    }
}
