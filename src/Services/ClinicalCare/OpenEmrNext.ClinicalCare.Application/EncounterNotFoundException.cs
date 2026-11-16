namespace OpenEmrNext.ClinicalCare.Application;

/// <summary>
/// The encounter vitals were requested for does not exist, or legacy has deleted it (CV-1). A missing thing is a
/// not-found, not a validation error, so the endpoint maps it to 404 (DOM-17).
/// </summary>
public sealed class EncounterNotFoundException(Guid encounterUuid)
    : Exception($"No encounter with uuid {encounterUuid} exists.")
{
    public Guid EncounterUuid { get; } = encounterUuid;
}
