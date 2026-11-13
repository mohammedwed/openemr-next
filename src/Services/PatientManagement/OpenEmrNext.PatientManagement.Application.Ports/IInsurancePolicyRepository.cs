using OpenEmrNext.PatientManagement.Domain.Insurance;

namespace OpenEmrNext.PatientManagement.Application.Ports;

/// <summary>
/// What the insurance use cases need from persistence. <see cref="GetRowsAsync"/> returns raw,
/// unparsed rows deliberately -- <see cref="EffectiveInsuranceRule"/> compares dates as stored, the
/// same way SQL compares a DATE column, and a pre-existing row (written before this service existed)
/// may hold a value (<c>0000-00-00</c>, a partial date) that doesn't parse as a real date at all.
/// </summary>
public interface IInsurancePolicyRepository
{
    Task AddAsync(InsurancePolicy policy, CancellationToken ct);

    Task<IReadOnlyList<InsurancePolicyRow>> GetRowsAsync(long pid, CancellationToken ct);

    /// <summary>
    /// Swaps which level <paramref name="policyUuid"/> occupies: it moves to
    /// <paramref name="targetLevel"/>, and whatever policy currently occupies that level (if any)
    /// takes the moved policy's old level instead. Matches legacy OpenEMR's own
    /// <c>InsuranceService::swapInsurance()</c>, including its atomicity -- unlike patient merge,
    /// this one already is transactional in legacy, and stays that way here.
    /// </summary>
    Task SwapAsync(long pid, InsuranceLevel targetLevel, Guid policyUuid, CancellationToken ct);
}
