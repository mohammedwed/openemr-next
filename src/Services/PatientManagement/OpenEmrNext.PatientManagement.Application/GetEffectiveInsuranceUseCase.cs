using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Domain.Insurance;
using OpenEmrNext.PatientManagement.Domain.Insurance.ValueObjects;
using OpenEmrNext.PatientManagement.Domain.Insurance.Rules;
using OpenEmrNext.PatientManagement.Domain.Insurance.ReadModels;

namespace OpenEmrNext.PatientManagement.Application;

public sealed class GetEffectiveInsuranceUseCase(IInsurancePolicyRepository repository)
{
    public async Task<IReadOnlyList<EffectiveInsurancePolicy>> ExecuteAsync(long pid, DateOnly day, CancellationToken ct)
    {
        var rows = await repository.GetRowsAsync(pid, ct);
        return EffectiveInsuranceRule.Choose(rows, day);
    }
}
