namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>
/// The outbox payload for <c>ProblemRecorded</c>: a deliberately curated set of fields (DOM-16, EVT-5). It carries no title, diagnosis or comment:
/// those are clinical content, and a consumer that needs them asks the API. Both the patient's uuid and pid travel while legacy tables still join
/// on <c>pid</c> (design set decision D1).
/// </summary>
public sealed record ProblemRecordedEvent(
    Guid ProblemUuid,
    Guid PatientUuid,
    long PatientPid,
    DateTime RecordedAtUtc)
{
    public const string EventType = "ProblemRecorded";
}
