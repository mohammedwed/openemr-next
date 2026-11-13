namespace OpenEmrNext.PatientManagement.Domain.Insurance;

/// <summary>Raised when a new insurance policy fails <see cref="InsurancePolicy.Add"/>'s rules.</summary>
public sealed class InsurancePolicyValidationException(IReadOnlyList<string> errors)
    : Exception(string.Join("; ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
