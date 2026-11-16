namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>
/// The outbox payload for <c>VitalsRecorded</c> (CV-13): curated (DOM-16, EVT-5). It says <b>which</b> measurements were
/// taken, not their values: vitals are clinical data, and a consumer that needs a value asks the API, which is the place
/// access will be controlled (finding F-CC-2).
/// </summary>
public sealed record VitalsRecordedEvent(
    Guid VitalsUuid,
    Guid EncounterUuid,
    long EncounterNumber,
    Guid PatientUuid,
    long PatientPid,
    IReadOnlyList<string> MeasurementsTaken,
    DateTime RecordedAtUtc)
{
    public const string EventType = "VitalsRecorded";
}
