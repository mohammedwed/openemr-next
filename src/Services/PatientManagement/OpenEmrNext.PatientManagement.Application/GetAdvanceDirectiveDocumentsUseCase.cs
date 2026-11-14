using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives;
using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives.ValueObjects;
using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives.ReadModels;

namespace OpenEmrNext.PatientManagement.Application;

/// <summary>Lists a patient's advance-directive documents (summaries only, not file content -- see
/// <see cref="DownloadAdvanceDirectiveDocumentUseCase"/> for that), including legacy's pre-existing
/// rows this service didn't create itself, which classify as <c>Type = null</c>, legacy's own generic
/// "Advance Directive" bucket, rather than one of the three keyword-matched types.</summary>
public sealed class GetAdvanceDirectiveDocumentsUseCase(IAdvanceDirectiveRepository repository)
{
    public Task<IReadOnlyList<AdvanceDirectiveDocumentSummary>> ExecuteAsync(long pid, CancellationToken ct) =>
        repository.GetDocumentsAsync(pid, ct);
}
