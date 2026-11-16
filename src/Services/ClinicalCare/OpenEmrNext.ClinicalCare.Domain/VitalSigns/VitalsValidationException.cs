namespace OpenEmrNext.ClinicalCare.Domain.VitalSigns;

/// <summary>
/// Raised when a vitals recording fails <see cref="VitalsRecording.Create"/>'s rules. Carries every violation found,
/// not just the first (DOM-3), so an endpoint can return them all at once.
/// </summary>
public sealed class VitalsValidationException(IReadOnlyList<string> errors) : Exception(string.Join("; ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
