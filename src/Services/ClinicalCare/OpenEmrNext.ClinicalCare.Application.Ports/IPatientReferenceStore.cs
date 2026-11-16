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
    Task<EventRecordingResult> RecordPatientRegisteredAsync(
        Guid eventId, PatientReference patient, DateTime? registeredAtUtc, CancellationToken ct);
}
