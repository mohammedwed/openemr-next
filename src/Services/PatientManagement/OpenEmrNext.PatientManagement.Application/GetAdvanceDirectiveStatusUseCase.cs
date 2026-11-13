using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives;

namespace OpenEmrNext.PatientManagement.Application;

public sealed class GetAdvanceDirectiveStatusUseCase(IAdvanceDirectiveRepository repository)
{
    public Task<(AdvanceDirectiveCompletionStatus Status, DateTime? ReviewedAtUtc, long? AuthenticatorUserId)> ExecuteAsync(
        long pid, CancellationToken ct) =>
        repository.GetStatusAsync(pid, ct);
}
