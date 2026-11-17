using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;
using OpenEmrNext.ClinicalCare.Domain.VitalSigns;

namespace OpenEmrNext.ClinicalCare.Application;

/// <summary>The wire names of the measurements a correction can change, as audit rows and events list them (names, never values; SEC-9, EVT-5).</summary>
public static class VitalsMeasurementNames
{
    public const string Weight = "weight";
    public const string Height = "height";
    public const string Temperature = "temperature";
    public const string TemperatureMethod = "temperatureMethod";
    public const string BloodPressureSystolic = "bloodPressureSystolic";
    public const string BloodPressureDiastolic = "bloodPressureDiastolic";
    public const string Pulse = "pulse";
    public const string Respiration = "respiration";
    public const string OxygenSaturation = "oxygenSaturation";
    public const string WaistCircumference = "waistCircumference";
    public const string HeadCircumference = "headCircumference";
    public const string Note = "note";
}

/// <summary>
/// Which measurements a <see cref="VitalsChange"/> would actually change, compared with the stored view at storage precision (every stored measurement is already rounded to six places, so
/// equality is exact). A pure function, so "did anything change?" has one answer for the event, the audit row and the decision to write at all. The derived BMI is not listed: it moves only
/// when the weight or height does, and those are.
/// </summary>
public static class VitalsChangeSet
{
    /// <returns>The names of the measurements that differ, in a stable order; empty when the correction would leave the record as it is.</returns>
    public static IReadOnlyList<string> Between(VitalsView current, VitalsChange change)
    {
        var changed = new List<string>();

        if (current.WeightPounds != change.WeightPounds) changed.Add(VitalsMeasurementNames.Weight);
        if (current.HeightInches != change.HeightInches) changed.Add(VitalsMeasurementNames.Height);
        if (current.TemperatureFahrenheit != change.TemperatureFahrenheit) changed.Add(VitalsMeasurementNames.Temperature);
        if (!SameText(current.TemperatureMethod, change.TemperatureMethod)) changed.Add(VitalsMeasurementNames.TemperatureMethod);
        if (current.Systolic != change.Systolic) changed.Add(VitalsMeasurementNames.BloodPressureSystolic);
        if (current.Diastolic != change.Diastolic) changed.Add(VitalsMeasurementNames.BloodPressureDiastolic);
        if (current.Pulse != change.Pulse) changed.Add(VitalsMeasurementNames.Pulse);
        if (current.Respiration != change.Respiration) changed.Add(VitalsMeasurementNames.Respiration);
        if (current.OxygenSaturation != change.OxygenSaturation) changed.Add(VitalsMeasurementNames.OxygenSaturation);
        if (current.WaistInches != change.WaistInches) changed.Add(VitalsMeasurementNames.WaistCircumference);
        if (current.HeadInches != change.HeadInches) changed.Add(VitalsMeasurementNames.HeadCircumference);
        if (!SameText(current.Note, change.Note)) changed.Add(VitalsMeasurementNames.Note);

        return changed;
    }

    /// <summary>Legacy stores a blank text as either NULL or an empty string; the two are the same value.</summary>
    private static bool SameText(string? a, string? b) => string.Equals(a ?? "", b ?? "", StringComparison.Ordinal);
}

/// <summary>
/// Everything a caller supplies to correct vitals: which, in which encounter, the version the caller read, and the whole measurement set. Raw facts:
/// <c>VitalsChange.Create</c> owns the rules.
/// </summary>
/// <param name="Offered">The tags the caller sent in <c>If-Match</c>; the correction applies only if one is the current version (LEG-10).</param>
public sealed record CorrectVitalsCommand(
    Guid EncounterUuid, Guid VitalsUuid, IReadOnlyList<ConcurrencyToken> Offered, VitalsInput Input, RequestOrigin? Origin = null);

/// <summary>What a correction did: the vitals as they now stand, and which measurements changed (empty when none did).</summary>
public sealed record VitalsCorrectionResult(VitalsView Vitals, IReadOnlyList<string> ChangedMeasurements);

/// <summary>
/// Corrects recorded vitals (M5d slice 1, ADR 0068). Thin by design (ARC-4): find the encounter and the vitals, check the caller's version, let the domain validate, work out what changed,
/// and hand the rest to <see cref="IVitalsUpdater"/> to commit with its event and audit row. The same order of checks as the encounter and issue updates; a refused correction is audited as
/// failed. Legacy edits vitals in place with no history, and so does this: the audit row says which measurements changed, not what they were.
/// </summary>
public sealed class CorrectVitalsUseCase(
    IEncounterReader encounters,
    IVitalsReader vitals,
    IVitalsUpdater updater,
    IAuditLog auditLog,
    TimeProvider clock)
{
    /// <exception cref="EncounterNotFoundException">No such encounter, or legacy deleted it. A <c>failed</c> audit row is written.</exception>
    /// <exception cref="VitalsNotFoundException">No such vitals in this encounter. A <c>failed</c> audit row is written.</exception>
    /// <exception cref="ConcurrencyConflictException">The vitals are no longer the version the caller read. Nothing is written; a <c>failed</c> audit row is.</exception>
    /// <exception cref="VitalsValidationException">A measurement fails validation. Nothing is written; a <c>failed</c> audit row is.</exception>
    public async Task<VitalsCorrectionResult> ExecuteAsync(CorrectVitalsCommand command, CancellationToken ct)
    {
        try
        {
            return await CorrectAsync(command, ct);
        }
        catch (Exception ex) when (ex is EncounterNotFoundException or VitalsNotFoundException or ConcurrencyConflictException or VitalsValidationException)
        {
            // A refused correction changed nothing, so its audit row is its own statement (ADR 0069 #4); it fails closed like the others.
            await auditLog.AppendAsync(
                new AuditEntry(
                    clock.GetUtcNow().UtcDateTime, Actors.Of(command.Origin), AuditActions.VitalsCorrect, command.VitalsUuid, AuditOutcome.Failed,
                    command.Origin?.CorrelationId, command.Origin?.SourceAddress),
                ct);
            throw;
        }
    }

    private async Task<VitalsCorrectionResult> CorrectAsync(CorrectVitalsCommand command, CancellationToken ct)
    {
        var encounterView = await encounters.FindAsync(command.EncounterUuid, ct)
            ?? throw new EncounterNotFoundException(command.EncounterUuid);

        // An encounter with no number or no known patient cannot own vitals, so there is nothing here to correct.
        var encounter = EncounterReference.TryCreate(encounterView.Uuid, encounterView.Number, encounterView.PatientUuid, encounterView.PatientPid)
            ?? throw new VitalsNotFoundException(command.VitalsUuid);

        var current = await vitals.FindAsync(encounter, command.VitalsUuid, ct)
            ?? throw new VitalsNotFoundException(command.VitalsUuid);

        // The precondition comes before the request is judged: an out-of-date caller is told so, whatever else is wrong with it. A row with no stamp cannot be verified.
        if (current.LastUpdated is not { } stamp || !command.Offered.Contains(ConcurrencyToken.From(stamp)))
            throw new ConcurrencyConflictException(command.VitalsUuid, current.LastUpdated);

        var change = VitalsChange.Create(command.Input);

        var changed = VitalsChangeSet.Between(current, change);
        if (changed.Count == 0)
            return new VitalsCorrectionResult(current, changed);   // nothing to write, so no event and no audit row: nothing happened

        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var outcome = await updater.UpdateAsync(
            new VitalsUpdateRequest(
                command.VitalsUuid, encounter.Number, encounter.Patient.Pid, ConcurrencyToken.From(stamp), change,
                new VitalsCorrectedEvent(
                    command.VitalsUuid, encounter.Uuid, encounter.Number, encounter.Patient.Uuid, encounter.Patient.Pid, changed, nowUtc),
                new AuditEntry(
                    nowUtc, Actors.Of(command.Origin), AuditActions.VitalsCorrect, command.VitalsUuid, AuditOutcome.Success,
                    command.Origin?.CorrelationId, command.Origin?.SourceAddress, changed)),
            ct);

        return outcome.Status switch
        {
            // The vitals are read again so the caller gets exactly what is stored, including the new stamp and the recomputed BMI.
            UpdateStatus.Updated => new VitalsCorrectionResult(
                await vitals.FindAsync(encounter, command.VitalsUuid, ct) ?? throw new VitalsNotFoundException(command.VitalsUuid), changed),
            UpdateStatus.Conflict => throw new ConcurrencyConflictException(command.VitalsUuid, outcome.Stamp),
            _ => throw new VitalsNotFoundException(command.VitalsUuid)
        };
    }
}
