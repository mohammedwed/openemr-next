using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;
using OpenEmrNext.ClinicalCare.Domain.VitalSigns;

namespace OpenEmrNext.ClinicalCare.Application;

/// <summary>Everything a caller supplies to record vitals. Raw facts: <c>VitalsRecording.Create</c> owns the rules.</summary>
public sealed record RecordVitalsCommand(Guid EncounterUuid, VitalsInput Input, long ProviderId, RequestOrigin? Origin = null);

/// <summary>
/// Records vitals against an existing encounter (slice 2). Thin by design (ARC-4): find the encounter, let the domain
/// decide, hand the result to <see cref="IVitalsRegistry"/> to commit with its event.
/// </summary>
public sealed class RecordVitalsUseCase(
    IEncounterReader encounters,
    IVitalsIdentityAllocator identities,
    IVitalsRegistry registry,
    TimeProvider clock)
{
    /// <exception cref="EncounterNotFoundException">No such encounter, or legacy deleted it (CV-1). Nothing is persisted.</exception>
    /// <exception cref="VitalsValidationException">One or more fields fail validation, or the encounter cannot take vitals. Nothing is persisted.</exception>
    public async Task<Vitals> ExecuteAsync(RecordVitalsCommand command, CancellationToken ct)
    {
        var encounterView = await encounters.FindAsync(command.EncounterUuid, ct)
            ?? throw new EncounterNotFoundException(command.EncounterUuid);

        var encounter = EncounterReference.Create(
            encounterView.Uuid, encounterView.Number, encounterView.PatientUuid, encounterView.PatientPid);

        // Same clock rule as encounters: legacy datetime columns hold server-local time (LEG-11, unverified).
        var recording = VitalsRecording.Create(
            encounter, command.Input, command.ProviderId, clock.GetLocalNow().DateTime);

        var vitals = Vitals.Record(recording, identities.Allocate());

        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var audit = new AuditEntry(
            nowUtc, new AuditActor($"user:{command.ProviderId}", command.ProviderId, Verified: false), AuditActions.VitalsRecord,
            vitals.Uuid, AuditOutcome.Success, command.Origin?.CorrelationId, command.Origin?.SourceAddress);

        await registry.RegisterAsync(
            vitals,
            new VitalsRecordedEvent(
                vitals.Uuid, encounter.Uuid, encounter.Number, encounter.Patient.Uuid, encounter.Patient.Pid,
                vitals.MeasurementsTaken, nowUtc),
            audit,
            ct);

        return vitals;
    }
}
