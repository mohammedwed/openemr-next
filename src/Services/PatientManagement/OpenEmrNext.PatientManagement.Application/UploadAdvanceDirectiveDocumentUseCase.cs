using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives;
using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives.ValueObjects;
using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives.ReadModels;

namespace OpenEmrNext.PatientManagement.Application;

public sealed class UploadAdvanceDirectiveDocumentUseCase(IAdvanceDirectiveRepository repository)
{
    public async Task<AdvanceDirectiveDocument> ExecuteAsync(
        long pid, AdvanceDirectiveType type, string originalFileName, string mimeType,
        byte[] content, DateOnly? effectiveDate, long ownerId, CancellationToken ct)
    {
        var document = AdvanceDirectiveDocument.Upload(pid, type, originalFileName, mimeType, content, effectiveDate, ownerId);
        await repository.AddDocumentAsync(document, ct);
        return document;
    }
}
