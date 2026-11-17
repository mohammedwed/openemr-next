using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Application;

/// <summary>What Clinical Care needs from a <c>PatientRegistered</c> event: who, and when. The names in the event are not used.</summary>
public sealed record PatientRegisteredMessage(Guid EventId, Guid PatientUuid, long PatientPid, DateTime? RegisteredAtUtc);

/// <summary>
/// Consumes <c>PatientRegistered</c> (slice 3, design set R7): remembers that the patient exists so an encounter can be
/// created for them without asking another service or reading its tables. Idempotent on the event id (EVT-3).
/// </summary>
public sealed class ConsumePatientRegisteredUseCase(IPatientReferenceStore store, TimeProvider clock)
{
    /// <summary>The earliest registration time believed (the same floor as encounter dates).</summary>
    private static readonly DateTime EarliestPlausible = new(1900, 1, 1);

    /// <exception cref="ClinicalCareValidationException">The message names no usable patient (empty uuid or non-positive pid).</exception>
    public Task<EventRecordingResult> ExecuteAsync(PatientRegisteredMessage message, CancellationToken ct)
    {
        if (message.EventId == Guid.Empty)
            throw new ClinicalCareValidationException(["The event has no eventId, so it cannot be processed safely twice."]);

        // The same factory every other path uses: one owner of "what is a valid patient reference" (DOM-2).
        var patient = PatientReference.Create(message.PatientUuid, message.PatientPid);

        // registered_at is informational. A producer's nonsense timestamp must not fail, retry and finally dead-letter an event whose
        // patient identity is perfectly good (review finding R-5), so an implausible time is recorded as unknown (a tolerant reader).
        var registeredAt = message.RegisteredAtUtc is { } at && at >= EarliestPlausible && at <= clock.GetUtcNow().UtcDateTime.AddDays(1)
            ? at
            : (DateTime?)null;

        return store.RecordPatientRegisteredAsync(message.EventId, patient, registeredAt, ct);
    }
}
