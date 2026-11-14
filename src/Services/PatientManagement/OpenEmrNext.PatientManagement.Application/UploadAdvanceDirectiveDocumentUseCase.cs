using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives;
using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives.ValueObjects;
using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives.ReadModels;

namespace OpenEmrNext.PatientManagement.Application;

/// <summary>
/// Uploads a new advance-directive document: validates via <see cref="AdvanceDirectiveDocument.Upload"/>
/// (file-name, mime-type, and size-bound rules, including <see cref="AdvanceDirectiveDocument.MaxContentBytes"/>),
/// then hands it to the port for persistence -- the port's own adapter composes the document-metadata
/// write (legacy's <c>documents</c>/<c>categories_to_documents</c> tables) and the file-bytes write
/// (local disk) into one operation; this use case knows about neither.
/// </summary>
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
