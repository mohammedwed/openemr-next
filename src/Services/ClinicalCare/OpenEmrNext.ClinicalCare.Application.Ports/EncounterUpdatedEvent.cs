namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>
/// The outbox payload for <c>EncounterUpdated</c>: which fields changed, never their values (DOM-16, EVT-5). A consumer that needs the new
/// state asks the API. The patient and encounter keys are nullable because an update can reach a legacy-created row that lacks them.
/// </summary>
public sealed record EncounterUpdatedEvent(
    Guid EncounterUuid,
    long? EncounterNumber,
    Guid? PatientUuid,
    long? PatientPid,
    IReadOnlyList<string> ChangedFields,
    DateTime UpdatedAtUtc)
{
    public const string EventType = "EncounterUpdated";
}
