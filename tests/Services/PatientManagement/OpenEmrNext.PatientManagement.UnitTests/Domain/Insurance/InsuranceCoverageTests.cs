using OpenEmrNext.PatientManagement.Domain.Insurance;
using OpenEmrNext.PatientManagement.Domain.Insurance.ValueObjects;

namespace OpenEmrNext.PatientManagement.UnitTests.Domain.Insurance;

public class InsuranceCoverageTests
{
    private const long Pid = 1;
    private static readonly DateOnly StartDate = new(2026, 1, 1);
    private static readonly DateOnly LaterStartDate = new(2026, 6, 1);

    private static InsurancePolicy ReconstitutedPolicy(InsuranceLevel level, DateOnly startDate) =>
        InsurancePolicy.Reconstitute(
            id: 1, uuid: Guid.NewGuid(), pid: Pid, level, companyId: 42,
            policyNumber: "POL123", groupNumber: null, planName: null, startDate, endDate: null);

    [Fact]
    public void AddPolicy_with_a_fresh_level_and_start_date_succeeds()
    {
        var coverage = InsuranceCoverage.For(Pid, []);

        var policy = coverage.AddPolicy(
            InsuranceLevel.Primary, companyId: 42, "POL123", null, null, StartDate, null);

        Assert.Equal(InsuranceLevel.Primary, policy.Level);
        Assert.Contains(policy, coverage.Policies);
    }

    [Fact]
    public void AddPolicy_rejects_a_level_and_start_date_already_occupied()
    {
        var existing = ReconstitutedPolicy(InsuranceLevel.Primary, StartDate);
        var coverage = InsuranceCoverage.For(Pid, [existing]);

        Assert.Throws<InsurancePolicyValidationException>(() =>
            coverage.AddPolicy(InsuranceLevel.Primary, 42, "POL999", null, null, StartDate, null));
    }

    [Fact]
    public void AddPolicy_allows_the_same_level_with_a_different_start_date()
    {
        var existing = ReconstitutedPolicy(InsuranceLevel.Primary, StartDate);
        var coverage = InsuranceCoverage.For(Pid, [existing]);

        var policy = coverage.AddPolicy(InsuranceLevel.Primary, 42, "POL999", null, null, LaterStartDate, null);

        Assert.Equal(2, coverage.Policies.Count);
        Assert.Equal(LaterStartDate, policy.StartDate);
    }

    [Fact]
    public void Swap_moves_the_source_policy_to_the_target_level_when_it_is_empty()
    {
        var source = ReconstitutedPolicy(InsuranceLevel.Secondary, StartDate);
        var coverage = InsuranceCoverage.For(Pid, [source]);

        var result = coverage.Swap(source.Uuid, InsuranceLevel.Primary);

        Assert.Equal(InsuranceLevel.Primary, source.Level);
        Assert.Same(source, result.Source);
        Assert.Null(result.DisplacedOccupant);
    }

    [Fact]
    public void Swap_trades_levels_with_whichever_policy_occupies_the_target_level()
    {
        var source = ReconstitutedPolicy(InsuranceLevel.Secondary, StartDate);
        var occupant = ReconstitutedPolicy(InsuranceLevel.Primary, LaterStartDate);
        var coverage = InsuranceCoverage.For(Pid, [source, occupant]);

        var result = coverage.Swap(source.Uuid, InsuranceLevel.Primary);

        Assert.Equal(InsuranceLevel.Primary, source.Level);
        Assert.Equal(InsuranceLevel.Secondary, occupant.Level);
        Assert.Same(occupant, result.DisplacedOccupant);
    }

    [Fact]
    public void Swap_picks_the_most_recently_started_policy_when_several_occupy_the_target_level()
    {
        var source = ReconstitutedPolicy(InsuranceLevel.Secondary, StartDate);
        var older = ReconstitutedPolicy(InsuranceLevel.Primary, StartDate);
        var newer = ReconstitutedPolicy(InsuranceLevel.Primary, LaterStartDate);
        var coverage = InsuranceCoverage.For(Pid, [source, older, newer]);

        var result = coverage.Swap(source.Uuid, InsuranceLevel.Primary);

        Assert.Same(newer, result.DisplacedOccupant);
        Assert.Equal(InsuranceLevel.Secondary, newer.Level);
        Assert.Equal(InsuranceLevel.Primary, older.Level);
    }

    [Fact]
    public void Swap_is_a_no_op_when_the_policy_is_already_at_the_target_level()
    {
        var source = ReconstitutedPolicy(InsuranceLevel.Primary, StartDate);
        var coverage = InsuranceCoverage.For(Pid, [source]);

        var result = coverage.Swap(source.Uuid, InsuranceLevel.Primary);

        Assert.Equal(InsuranceLevel.Primary, source.Level);
        Assert.Null(result.DisplacedOccupant);
    }

    [Fact]
    public void Swap_throws_when_the_policy_is_not_in_this_patients_coverage()
    {
        var coverage = InsuranceCoverage.For(Pid, []);

        Assert.Throws<KeyNotFoundException>(() => coverage.Swap(Guid.NewGuid(), InsuranceLevel.Primary));
    }
}
