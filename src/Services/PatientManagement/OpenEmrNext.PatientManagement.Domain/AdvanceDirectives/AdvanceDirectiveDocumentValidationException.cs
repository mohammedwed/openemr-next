namespace OpenEmrNext.PatientManagement.Domain.AdvanceDirectives;

/// <summary>Raised when a new advance directive document fails <see cref="AdvanceDirectiveDocument.Upload"/>'s rules.</summary>
public sealed class AdvanceDirectiveDocumentValidationException(IReadOnlyList<string> errors)
    : Exception(string.Join("; ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
