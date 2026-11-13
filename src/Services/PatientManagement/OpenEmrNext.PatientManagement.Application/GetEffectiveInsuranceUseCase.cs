using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Domain.Insurance;

namespace OpenEmrNext.PatientManagement.Application;

public sealed class GetEffectiveInsuranceUseCase(IInsurancePolicyRepository repository)
{
    public async Task<IReadOnlyList<EffectiveInsurancePolicy>> ExecuteAsync(long pid, DateOnly day, CancellationToken ct)
    {
        var rows = await repository.GetRowsAsync(pid, ct);
        return EffectiveInsuranceRule.Choose(rows, day);
    }
}
