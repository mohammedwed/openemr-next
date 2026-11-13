using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Domain.Insurance;

namespace OpenEmrNext.PatientManagement.Application;

public sealed class SwapInsuranceUseCase(IInsurancePolicyRepository repository)
{
    public Task ExecuteAsync(long pid, InsuranceLevel targetLevel, Guid policyUuid, CancellationToken ct) =>
        repository.SwapAsync(pid, targetLevel, policyUuid, ct);
}
