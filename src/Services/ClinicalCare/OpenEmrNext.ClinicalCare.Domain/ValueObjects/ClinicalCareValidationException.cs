namespace OpenEmrNext.ClinicalCare.Domain.ValueObjects;

/// <summary>
/// Raised when something Clinical Care builds or accepts is invalid and the caller can fix it: a new encounter that fails
/// <see cref="EncounterCreation.Create"/>'s rules, a <see cref="PatientReference"/> or <see cref="EncounterIdentity"/> built from
/// bad input, a paging request out of bounds, an event naming no usable patient. Carries every violation found, not just the
/// first (DOM-3), so an endpoint can return them all at once.
/// <para>
/// Named for the service, not for encounters: it was <c>EncounterValidationException</c> until the layered review (finding R-13)
/// noted that it was thrown for things that are not encounters. Vitals keep their own, <see cref="VitalSigns.VitalsValidationException"/>,
/// because they are a separate area with their own rules.
/// </para>
/// </summary>
public sealed class ClinicalCareValidationException(IReadOnlyList<string> errors)
    : Exception(string.Join("; ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
