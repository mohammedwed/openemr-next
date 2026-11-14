using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Domain.Insurance;
using OpenEmrNext.PatientManagement.Domain.Insurance.ValueObjects;
using OpenEmrNext.PatientManagement.Domain.Insurance.Rules;
using OpenEmrNext.PatientManagement.Domain.Insurance.ReadModels;

namespace OpenEmrNext.PatientManagement.Application;

/// <summary>
/// Moves one policy to <c>targetLevel</c>, trading places with whatever already occupies it --
/// orchestrates loading the patient's whole <see cref="InsuranceCoverage"/> cluster, letting
/// <see cref="InsuranceCoverage.Swap"/> decide both policies' new levels, then persisting that
/// decision (ADR 0053). The decision logic itself lives entirely in the aggregate; this use case is
/// just the load/decide/persist orchestration.
/// </summary>
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
