using OpenEmrNext.PatientManagement.Domain.Insurance.ValueObjects;

namespace OpenEmrNext.PatientManagement.Domain.Insurance;

/// <summary>
/// One patient's whole cluster of insurance policies -- the real aggregate boundary legacy's own
/// <c>InsuranceService::swapInsurance()</c> already needs, just not expressed as one in legacy (or,
/// until now, here): a swap genuinely touches two policies at once (the one moving, and whatever
/// already occupies the target level), and "which policy ends up at which level" is a business
/// decision, not a SQL detail. That decision now lives here; Infrastructure's job shrinks to
/// persisting whatever this aggregate decided, including the <c>9999-12-31</c>
/// sentinel-date workaround for the <c>(pid, type, date)</c> unique constraint -- which really is a
/// pure persistence concern (the database's own constraint, not a rule this patient's coverage
/// cares about), so it stays out of this class entirely.
/// </summary>
/// <remarks>
/// <see cref="InsurancePolicy"/> stays the entity with real identity (<c>Id</c>/<c>Uuid</c>) and its
/// own invariants (<see cref="InsurancePolicy.Add"/>); this aggregate only adds the invariant that
/// only makes sense across the whole cluster -- no two policies at the same level with the same
/// start date -- and owns the swap decision that reads two policies to decide both their new levels.
/// </remarks>
public sealed class InsuranceCoverage
{
    public long Pid { get; }

    private readonly List<InsurancePolicy> _policies;
    public IReadOnlyList<InsurancePolicy> Policies => _policies;

    private InsuranceCoverage(long pid, List<InsurancePolicy> policies)
    {
        Pid = pid;
        _policies = policies;
    }

    /// <summary>Rebuilds the cluster from whatever policies already exist for this patient --
    /// there's no "create" for a patient's coverage as a whole, only ever "here's what currently
    /// exists," the same way a shopping cart aggregate is loaded from its existing line items rather
    /// than instantiated from scratch.</summary>
    public static InsuranceCoverage For(long pid, IEnumerable<InsurancePolicy> existingPolicies) =>
        new(pid, [.. existingPolicies]);

    /// <exception cref="InsurancePolicyValidationException">A policy already occupies this exact
    /// level and start date, or the new policy's own fields fail <see cref="InsurancePolicy.Add"/>'s
    /// rules.</exception>
    public InsurancePolicy AddPolicy(
        InsuranceLevel level, long companyId, string policyNumber,
        string? groupNumber, string? planName, DateOnly startDate, DateOnly? endDate)
    {
        if (_policies.Any(p => p.Level == level && p.StartDate == startDate))
            throw new InsurancePolicyValidationException(
                ["A policy already exists at this level with this start date."]);

        var policy = InsurancePolicy.Add(Pid, level, companyId, policyNumber, groupNumber, planName, startDate, endDate);
        _policies.Add(policy);
        return policy;
    }

    /// <summary>
    /// Decides the swap legacy's <c>swapInsurance()</c> performs: <paramref name="policyUuid"/> moves
    /// to <paramref name="targetLevel"/>, and whichever policy currently occupies that level (the one
    /// with the most recent start date, if more than one somehow does) takes the mover's old level
    /// instead. A no-op (nothing in <see cref="Policies"/> changes) if the policy is already at
    /// <paramref name="targetLevel"/>.
    /// </summary>
    /// <exception cref="KeyNotFoundException"><paramref name="policyUuid"/> isn't in this cluster.</exception>
    public InsuranceSwapResult Swap(Guid policyUuid, InsuranceLevel targetLevel)
    {
        var source = _policies.SingleOrDefault(p => p.Uuid == policyUuid)
            ?? throw new KeyNotFoundException($"No insurance policy with uuid {policyUuid} in this patient's coverage.");

        if (source.Level == targetLevel)
            return new InsuranceSwapResult(source, null);

        var target = _policies
            .Where(p => p.Level == targetLevel && p.Uuid != source.Uuid)
            .OrderByDescending(p => p.StartDate)
            .FirstOrDefault();

        var sourceOldLevel = source.Level;
        source.ChangeLevel(targetLevel);
        target?.ChangeLevel(sourceOldLevel);

        return new InsuranceSwapResult(source, target);
    }
}

/// <summary>The outcome of an <see cref="InsuranceCoverage.Swap"/>: the policy that moved, and
/// whichever policy it traded places with (<c>null</c> when the target level was empty).</summary>
public sealed record InsuranceSwapResult(InsurancePolicy Source, InsurancePolicy? DisplacedOccupant);
