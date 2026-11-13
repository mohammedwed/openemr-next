using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives;

namespace OpenEmrNext.PatientManagement.Application;

public sealed class SetAdvanceDirectiveStatusUseCase(IAdvanceDirectiveRepository repository)
{
    public Task ExecuteAsync(
        long pid, AdvanceDirectiveCompletionStatus status, DateTime? reviewedAtUtc,
        long? authenticatorUserId, CancellationToken ct) =>
        repository.SetStatusAsync(pid, status, reviewedAtUtc, authenticatorUserId, ct);
}
