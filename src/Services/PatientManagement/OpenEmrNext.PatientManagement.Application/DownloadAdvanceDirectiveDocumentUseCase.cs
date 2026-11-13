using OpenEmrNext.PatientManagement.Application.Ports;

namespace OpenEmrNext.PatientManagement.Application;

public sealed class DownloadAdvanceDirectiveDocumentUseCase(IAdvanceDirectiveRepository repository)
{
    public Task<(string MimeType, string FileName, byte[] Content)?> ExecuteAsync(long pid, Guid uuid, CancellationToken ct) =>
        repository.GetDocumentContentAsync(pid, uuid, ct);
}
