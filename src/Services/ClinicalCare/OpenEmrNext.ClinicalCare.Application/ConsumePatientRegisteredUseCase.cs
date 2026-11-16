using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Application;

/// <summary>What Clinical Care needs from a <c>PatientRegistered</c> event: who, and when. The names in the event are not used.</summary>
public sealed record PatientRegisteredMessage(Guid EventId, Guid PatientUuid, long PatientPid, DateTime? RegisteredAtUtc);

/// <summary>
/// Consumes <c>PatientRegistered</c> (slice 3, design set R7): remembers that the patient exists so an encounter can be
/// created for them without asking another service or reading its tables. Idempotent on the event id (EVT-3).
/// </summary>
public sealed class ConsumePatientRegisteredUseCase(IPatientReferenceStore store)
{
    /// <exception cref="EncounterValidationException">The message names no usable patient (empty uuid or non-positive pid).</exception>
    public Task<EventRecordingResult> ExecuteAsync(PatientRegisteredMessage message, CancellationToken ct)
    {
        if (message.EventId == Guid.Empty)
            throw new EncounterValidationException(["The event has no eventId, so it cannot be processed safely twice."]);

        // The same factory every other path uses: one owner of "what is a valid patient reference" (DOM-2).
        var patient = PatientReference.Create(message.PatientUuid, message.PatientPid);

        return store.RecordPatientRegisteredAsync(message.EventId, patient, message.RegisteredAtUtc, ct);
    }
}
