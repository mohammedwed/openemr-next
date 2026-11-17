using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>The outcome of recording an incoming event.</summary>
public enum EventRecordingResult
{
    /// <summary>The event was new and its effect was applied.</summary>
    Recorded,

    /// <summary>The event id had been processed before: nothing changed (EVT-3).</summary>
    AlreadyProcessed
}

/// <summary>
/// An event contradicts what Clinical Care already holds about a patient: the same pid under a different uuid, or the same uuid
/// under a different pid. Retrying the same event cannot succeed, so this is a **rejection of the event itself** (review finding
/// R-9), reported to the sender as 422 so its relay dead-letters it at once instead of retrying for about ten minutes while every
/// later event waits behind it.
/// </summary>
public sealed class PatientReferenceConflictException(string message) : Exception(message);

/// <summary>
/// Where Clinical Care keeps the patients it has been told about: a reference (uuid and pid) and nothing else, never a
/// copy of demographics (design set R7, decision D4). Fed by <c>PatientRegistered</c> events and, once, by the provisioning
/// backfill. Replaces the interim <c>patient_data</c> lookup (ADR 0064 #5).
/// </summary>
public interface IPatientReferenceStore
{
    /// <summary>
    /// Records that <paramref name="eventId"/> was processed and, in the <b>same transaction</b>, adds the patient if it is
    /// not yet known. A repeated <paramref name="eventId"/> is a no-op, so redelivery is harmless (EVT-3). A patient that is
    /// already known is left alone (an event may legitimately arrive after the backfill got there first).
    /// </summary>
    /// <exception cref="PatientReferenceConflictException">The event contradicts a reference already held (same pid, different uuid, or the reverse). Nothing is recorded, not even that the event was seen.</exception>
    Task<EventRecordingResult> RecordPatientRegisteredAsync(
        Guid eventId, PatientReference patient, DateTime? registeredAtUtc, CancellationToken ct);
}
