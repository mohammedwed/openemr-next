namespace OpenEmrNext.PatientManagement.Domain.Fhir.ValueObjects;

/// <summary>Raised when a new <see cref="ResolvedCode"/> fails <see cref="ResolvedCode.Create"/>'s rules.</summary>
public sealed class ResolvedCodeValidationException(IReadOnlyList<string> errors)
    : Exception(string.Join("; ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
