using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Domain.Insurance;
using OpenEmrNext.PatientManagement.Domain.Insurance.ValueObjects;
using OpenEmrNext.PatientManagement.Domain.Insurance.Rules;
using OpenEmrNext.PatientManagement.Domain.Insurance.ReadModels;

namespace OpenEmrNext.PatientManagement.Application;

/// <summary>
/// Which policy is in force for a patient, at each level, on a given day. Reads the raw stored rows
/// (<see cref="IInsurancePolicyRepository.GetRowsAsync"/>), not the typed <c>InsuranceCoverage</c>
/// cluster -- <see cref="EffectiveInsuranceRule"/> has to tolerate legacy's own zero/partial stored
/// dates, which a strict <see cref="DateOnly"/> cannot represent, and is itself the only place that
/// decision belongs.
/// </summary>
public sealed class GetEffectiveInsuranceUseCase(IInsurancePolicyRepository repository)
{
    public async Task<IReadOnlyList<EffectiveInsurancePolicy>> ExecuteAsync(long pid, DateOnly day, CancellationToken ct)
    {
        var rows = await repository.GetRowsAsync(pid, ct);
        return EffectiveInsuranceRule.Choose(rows, day);
    }
}
