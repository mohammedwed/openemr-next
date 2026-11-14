using OpenEmrNext.PatientManagement.Domain.Insurance;
using OpenEmrNext.PatientManagement.Domain.Insurance.ValueObjects;
using OpenEmrNext.PatientManagement.Domain.Insurance.Rules;
using OpenEmrNext.PatientManagement.Domain.Insurance.ReadModels;

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
    /// The same patient's policies as real reconstituted <see cref="InsurancePolicy"/> entities, for
    /// loading an <see cref="InsuranceCoverage"/> cluster -- unlike <see cref="GetRowsAsync"/>, this
    /// requires every row to have a real, parseable <see cref="DateOnly"/> (a pre-existing row with a
    /// legacy zero/partial date can't become a strict-typed entity), which is fine for the swap
    /// operation this exists for: both the policy being moved and whatever already occupies the
    /// target level are rows this service itself created, through <see cref="InsurancePolicy.Add"/>,
    /// which never allows a bad date through in the first place.
    /// </summary>
    Task<IReadOnlyList<InsurancePolicy>> GetPoliciesAsync(long pid, CancellationToken ct);

    /// <summary>
    /// Persists an <see cref="InsuranceCoverage.Swap"/> decision: the moved policy's new level, and
    /// the displaced occupant's (if any). Still where the <c>9999-12-31</c> sentinel-date workaround
    /// for the <c>(pid, type, date)</c> unique constraint lives -- that's a database detail, not
    /// something the swap decision itself needs to know about. Matches legacy OpenEMR's own
    /// <c>InsuranceService::swapInsurance()</c>, including its atomicity -- unlike patient merge,
    /// this one already is transactional in legacy, and stays that way here.
    /// </summary>
    Task PersistSwapAsync(InsuranceSwapResult result, CancellationToken ct);
}
