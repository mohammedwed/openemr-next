namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>
/// The outbox payload for <c>AllergyRecorded</c>: ids and the instant only (DOM-16, EVT-5). It carries no substance, reaction or comment: those are
/// clinical content, and a consumer that needs them asks the API. A downstream drug-interaction or alerting context is the likely consumer.
/// </summary>
public sealed record AllergyRecordedEvent(
    Guid AllergyUuid,
    Guid PatientUuid,
    long PatientPid,
    DateTime RecordedAtUtc)
{
    public const string EventType = "AllergyRecorded";
}
