namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>
/// The outbox payload for <c>EncounterCreated</c>: a deliberately curated set of fields, not the whole
/// <c>form_encounter</c> row (DOM-16, EVT-5). It carries no reason text or free-text clinical content; a
/// consumer that needs more asks the API. Both the patient's <c>uuid</c> and <c>pid</c> travel, per design
/// set decision D1, while legacy tables still join on <c>pid</c>.
/// </summary>
/// <remarks>
/// The envelope (event id, schema version, correlation) is added where the row is written, not here (EVT-2,
/// ADR 0064 #4).
/// </remarks>
public sealed record EncounterCreatedEvent(
    Guid EncounterUuid,
    long EncounterNumber,
    Guid PatientUuid,
    long PatientPid,
    string ClassCode,
    DateTime EncounterDate,
    DateTime CreatedAtUtc)
{
    public const string EventType = "EncounterCreated";
}
