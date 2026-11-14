using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Domain.Insurance;
using OpenEmrNext.PatientManagement.Domain.Insurance.ValueObjects;
using OpenEmrNext.PatientManagement.Domain.Insurance.Rules;
using OpenEmrNext.PatientManagement.Domain.Insurance.ReadModels;

namespace OpenEmrNext.PatientManagement.Application;

public sealed class SwapInsuranceUseCase(IInsurancePolicyRepository repository)
{
    public async Task ExecuteAsync(long pid, InsuranceLevel targetLevel, Guid policyUuid, CancellationToken ct)
    {
        var policies = await repository.GetPoliciesAsync(pid, ct);
        var coverage = InsuranceCoverage.For(pid, policies);
        var result = coverage.Swap(policyUuid, targetLevel);
        await repository.PersistSwapAsync(result, ct);
    }
}
