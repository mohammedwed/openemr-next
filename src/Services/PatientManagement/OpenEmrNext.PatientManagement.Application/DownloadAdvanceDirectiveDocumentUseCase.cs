using OpenEmrNext.PatientManagement.Application.Ports;

namespace OpenEmrNext.PatientManagement.Application;

/// <summary>
/// Downloads one advance-directive document's raw file bytes. The <c>pid</c>-ownership check -- so
/// one patient's document can never be fetched through another patient's uuid -- happens inside the
/// port itself (<see cref="IAdvanceDirectiveRepository.GetDocumentContentAsync"/> returns
/// <c>null</c> on any mismatch), not here.
/// </summary>
public sealed class DownloadAdvanceDirectiveDocumentUseCase(IAdvanceDirectiveRepository repository)
{
    public Task<(string MimeType, string FileName, byte[] Content)?> ExecuteAsync(long pid, Guid uuid, CancellationToken ct) =>
        repository.GetDocumentContentAsync(pid, uuid, ct);
}
