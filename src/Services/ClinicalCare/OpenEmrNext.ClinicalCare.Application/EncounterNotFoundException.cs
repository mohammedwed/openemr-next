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

/// <summary>The vitals to correct do not exist in this encounter, or legacy deleted them. Maps to 404 (DOM-17).</summary>
public sealed class VitalsNotFoundException(Guid vitalsUuid) : Exception($"No vitals with uuid {vitalsUuid} exist in this encounter.")
{
    public Guid VitalsUuid { get; } = vitalsUuid;
}

public sealed class ImmunizationNotFoundException(Guid immunizationUuid) : Exception($"No immunization with uuid {immunizationUuid} exists.")
{
    public Guid ImmunizationUuid { get; } = immunizationUuid;
}

/// <summary>The immunization changed after the caller read it (ADR 0072): maps to 412, carrying the current content version so the caller can reload.</summary>
public sealed class ImmunizationConflictException(Guid immunizationUuid, OpenEmrNext.ClinicalCare.Domain.ValueObjects.ContentVersion? currentVersion)
    : Exception($"{immunizationUuid} changed after it was read.")
{
    public Guid ImmunizationUuid { get; } = immunizationUuid;

    public OpenEmrNext.ClinicalCare.Domain.ValueObjects.ContentVersion? CurrentVersion { get; } = currentVersion;
}
