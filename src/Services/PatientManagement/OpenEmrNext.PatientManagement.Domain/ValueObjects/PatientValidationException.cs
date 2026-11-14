namespace OpenEmrNext.PatientManagement.Domain.ValueObjects;

/// <summary>Raised when a new <see cref="PatientRegistration"/> fails <see cref="PatientRegistration.Create"/>'s rules.</summary>
public sealed class PatientValidationException(IReadOnlyList<string> errors)
    : Exception(string.Join("; ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
