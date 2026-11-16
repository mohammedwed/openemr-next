namespace OpenEmrNext.ClinicalCare.Domain.ValueObjects;

/// <summary>
/// Raised when a new encounter fails <see cref="EncounterCreation.Create"/>'s rules (or when a value object
/// such as <see cref="PatientReference"/> or <see cref="EncounterIdentity"/> is built from invalid input).
/// Carries every violation found, not just the first (DOM-3), so an endpoint can return them all at once.
/// </summary>
public sealed class EncounterValidationException(IReadOnlyList<string> errors)
    : Exception(string.Join("; ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
