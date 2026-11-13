namespace OpenEmrNext.PatientManagement.Api.Domain;

/// <summary>Raised when a <see cref="PatientRegistration"/> fails <see cref="Patient.Register"/>'s rules.</summary>
public sealed class PatientValidationException(IReadOnlyList<string> errors)
    : Exception(string.Join("; ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
