using OpenEmrNext.PatientManagement.Domain.Insurance;
using OpenEmrNext.PatientManagement.Domain.Insurance.ValueObjects;
using OpenEmrNext.PatientManagement.Domain.Insurance.Rules;
using OpenEmrNext.PatientManagement.Domain.Insurance.ReadModels;

namespace OpenEmrNext.PatientManagement.UnitTests.Domain.Insurance;

public class InsurancePolicyTests
{
    private static readonly DateOnly StartDate = new(2026, 1, 1);

    [Fact]
    public void Add_with_valid_fields_succeeds()
    {
        var policy = InsurancePolicy.Add(
            pid: 1, InsuranceLevel.Primary, companyId: 42, "POL12345", "GRP1", "Acme Health", StartDate, null);

        Assert.Equal(1, policy.Pid);
        Assert.Equal(InsuranceLevel.Primary, policy.Level);
        Assert.Equal(42, policy.CompanyId);
        Assert.Equal("POL12345", policy.PolicyNumber);
        Assert.Equal(0, policy.Id);
        Assert.NotEqual(Guid.Empty, policy.Uuid);
    }

    [Fact]
    public void Add_rejects_a_pid_that_cannot_identify_a_patient() =>
        Assert.Throws<InsurancePolicyValidationException>(
            () => InsurancePolicy.Add(0, InsuranceLevel.Primary, 42, "POL123", null, null, StartDate, null));

    [Fact]
    public void Add_rejects_a_company_id_that_is_not_positive() =>
        Assert.Throws<InsurancePolicyValidationException>(
            () => InsurancePolicy.Add(1, InsuranceLevel.Primary, 0, "POL123", null, null, StartDate, null));

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    public void Add_rejects_a_policy_number_under_two_characters(string policyNumber) =>
        Assert.Throws<InsurancePolicyValidationException>(
            () => InsurancePolicy.Add(1, InsuranceLevel.Primary, 42, policyNumber, null, null, StartDate, null));

    [Fact]
    public void Add_accepts_no_group_number_or_plan_name() =>
        InsurancePolicy.Add(1, InsuranceLevel.Primary, 42, "POL123", null, null, StartDate, null);

    [Fact]
    public void Add_rejects_an_end_date_before_the_start_date() =>
        Assert.Throws<InsurancePolicyValidationException>(
            () => InsurancePolicy.Add(1, InsuranceLevel.Primary, 42, "POL123", null, null, StartDate, StartDate.AddDays(-1)));

    [Fact]
    public void AssignGeneratedIdentity_cannot_run_twice()
    {
        var policy = InsurancePolicy.Add(1, InsuranceLevel.Primary, 42, "POL123", null, null, StartDate, null);
        policy.AssignGeneratedIdentity(10);

        Assert.Throws<InvalidOperationException>(() => policy.AssignGeneratedIdentity(11));
    }

    [Fact]
    public void Reconstitute_restores_the_real_id_and_uuid_without_revalidating()
    {
        var uuid = Guid.NewGuid();

        var policy = InsurancePolicy.Reconstitute(
            id: 10, uuid, pid: 1, InsuranceLevel.Secondary, companyId: 42,
            policyNumber: "POL123", groupNumber: null, planName: null, StartDate, endDate: null);

        Assert.Equal(10, policy.Id);
        Assert.Equal(uuid, policy.Uuid);
        Assert.Equal(InsuranceLevel.Secondary, policy.Level);
    }

    [Fact]
    public void ChangeLevel_moves_the_policy_to_a_different_level()
    {
        var policy = InsurancePolicy.Reconstitute(
            id: 10, Guid.NewGuid(), pid: 1, InsuranceLevel.Primary, companyId: 42,
            policyNumber: "POL123", groupNumber: null, planName: null, StartDate, endDate: null);

        policy.ChangeLevel(InsuranceLevel.Tertiary);

        Assert.Equal(InsuranceLevel.Tertiary, policy.Level);
    }
}
