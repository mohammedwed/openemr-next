using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives;

namespace OpenEmrNext.PatientManagement.Application;

public sealed class GetAdvanceDirectiveDocumentsUseCase(IAdvanceDirectiveRepository repository)
{
    public Task<IReadOnlyList<AdvanceDirectiveDocumentSummary>> ExecuteAsync(long pid, CancellationToken ct) =>
        repository.GetDocumentsAsync(pid, ct);
}
