using Microsoft.EntityFrameworkCore;
using OpenEmrNext.PatientManagement.Application;
using OpenEmrNext.PatientManagement.Domain.Insurance;
using OpenEmrNext.PatientManagement.Domain.Insurance.ValueObjects;
using OpenEmrNext.PatientManagement.Domain.Insurance.Rules;
using OpenEmrNext.PatientManagement.Domain.Insurance.ReadModels;
using OpenEmrNext.PatientManagement.Infrastructure;
using Xunit;

namespace OpenEmrNext.PatientManagement.IntegrationTests;

/// <summary>
/// Proves insurance persistence against a real MariaDB: adding a policy, reading it back through
/// the effective-insurance rule, and the transactional swap -- including the collision case that
/// needs the <c>9999-12-31</c> sentinel-date workaround.
/// </summary>
[Collection("PatientDataContainer")]
public class InsurancePolicyTests(PatientDataContainerFixture fixture)
{
    private static long NextPid() => Random.Shared.NextInt64(1, long.MaxValue);

    [Fact]
    public async Task A_new_policy_is_effective_immediately_when_given_no_end_date()
    {
        var pid = NextPid();
        await using var context = fixture.CreateDbContext();
        var repository = new EfInsurancePolicyRepository(context);

        await new AddInsurancePolicyUseCase(repository).ExecuteAsync(
            pid, InsuranceLevel.Primary, companyId: 7, "POL1", null, null, new DateOnly(2020, 1, 1), null, CancellationToken.None);

        var effective = await new GetEffectiveInsuranceUseCase(repository)
            .ExecuteAsync(pid, DateOnly.FromDateTime(DateTime.UtcNow), CancellationToken.None);

        var policy = Assert.Single(effective);
        Assert.Equal(InsuranceLevel.Primary, policy.Level);
        Assert.Equal(7, policy.CompanyId);
        Assert.Null(policy.End);
    }

    [Fact]
    public async Task Swapping_into_an_empty_level_just_moves_the_policy()
    {
        var pid = NextPid();
        await using var context = fixture.CreateDbContext();
        var repository = new EfInsurancePolicyRepository(context);

        var policy = await new AddInsurancePolicyUseCase(repository).ExecuteAsync(
            pid, InsuranceLevel.Primary, 7, "POL1", null, null, new DateOnly(2020, 1, 1), null, CancellationToken.None);

        await new SwapInsuranceUseCase(repository).ExecuteAsync(pid, InsuranceLevel.Secondary, policy.Uuid, CancellationToken.None);

        var effective = await new GetEffectiveInsuranceUseCase(repository)
            .ExecuteAsync(pid, DateOnly.FromDateTime(DateTime.UtcNow), CancellationToken.None);
        var moved = Assert.Single(effective);
        Assert.Equal(InsuranceLevel.Secondary, moved.Level);
    }

    [Fact]
    public async Task Swapping_two_occupied_levels_trades_their_types()
    {
        var pid = NextPid();
        await using var context = fixture.CreateDbContext();
        var repository = new EfInsurancePolicyRepository(context);

        var primary = await new AddInsurancePolicyUseCase(repository).ExecuteAsync(
            pid, InsuranceLevel.Primary, 7, "POL-PRIMARY", null, null, new DateOnly(2020, 1, 1), null, CancellationToken.None);
        var secondary = await new AddInsurancePolicyUseCase(repository).ExecuteAsync(
            pid, InsuranceLevel.Secondary, 8, "POL-SECONDARY", null, null, new DateOnly(2021, 1, 1), null, CancellationToken.None);

        await new SwapInsuranceUseCase(repository).ExecuteAsync(pid, InsuranceLevel.Secondary, primary.Uuid, CancellationToken.None);

        var effective = await new GetEffectiveInsuranceUseCase(repository)
            .ExecuteAsync(pid, DateOnly.FromDateTime(DateTime.UtcNow), CancellationToken.None);

        Assert.Equal(2, effective.Count);
        var nowSecondary = Assert.Single(effective, p => p.Id == primary.Id);
        var nowPrimary = Assert.Single(effective, p => p.Id == secondary.Id);
        Assert.Equal(InsuranceLevel.Secondary, nowSecondary.Level);
        Assert.Equal(InsuranceLevel.Primary, nowPrimary.Level);
    }

    [Fact]
    public async Task Swapping_two_policies_with_the_same_start_date_survives_the_unique_constraint()
    {
        // Both on the same date -- the (pid, type, date) unique constraint would collide the
        // instant either row's type changes to match the other's, unless the sentinel-date
        // workaround actually works. This is the case the swap exists to handle correctly.
        var pid = NextPid();
        var sameDate = new DateOnly(2022, 6, 15);
        await using var context = fixture.CreateDbContext();
        var repository = new EfInsurancePolicyRepository(context);

        var primary = await new AddInsurancePolicyUseCase(repository).ExecuteAsync(
            pid, InsuranceLevel.Primary, 7, "POL-PRIMARY", null, null, sameDate, null, CancellationToken.None);
        var secondary = await new AddInsurancePolicyUseCase(repository).ExecuteAsync(
            pid, InsuranceLevel.Secondary, 8, "POL-SECONDARY", null, null, sameDate, null, CancellationToken.None);

        await new SwapInsuranceUseCase(repository).ExecuteAsync(pid, InsuranceLevel.Secondary, primary.Uuid, CancellationToken.None);

        var effective = await new GetEffectiveInsuranceUseCase(repository)
            .ExecuteAsync(pid, DateOnly.FromDateTime(DateTime.UtcNow), CancellationToken.None);

        Assert.Equal(2, effective.Count);
        Assert.Equal(InsuranceLevel.Secondary, Assert.Single(effective, p => p.Id == primary.Id).Level);
        Assert.Equal(InsuranceLevel.Primary, Assert.Single(effective, p => p.Id == secondary.Id).Level);

        // The occupant's original date must have survived the temporary 9999-12-31 parking --
        // not left stranded there once the swap committed.
        await using var verify = fixture.CreateDbContext();
        var secondaryDate = await verify.Database.SqlQueryRaw<DateTime>(
            "SELECT date AS Value FROM insurance_data WHERE id = {0}", secondary.Id).SingleAsync();
        Assert.Equal(sameDate, DateOnly.FromDateTime(secondaryDate));
    }

    [Fact]
    public async Task Adding_a_second_policy_at_the_same_level_and_start_date_is_rejected()
    {
        // AddInsurancePolicyUseCase must route through InsuranceCoverage.AddPolicy, not
        // InsurancePolicy.Add directly -- otherwise this collides with the (pid, type, date) unique
        // constraint as a raw, unhandled database exception instead of a clean validation error.
        var pid = NextPid();
        var sameDate = new DateOnly(2023, 3, 1);
        await using var context = fixture.CreateDbContext();
        var repository = new EfInsurancePolicyRepository(context);

        await new AddInsurancePolicyUseCase(repository).ExecuteAsync(
            pid, InsuranceLevel.Primary, 7, "POL1", null, null, sameDate, null, CancellationToken.None);

        await Assert.ThrowsAsync<InsurancePolicyValidationException>(() =>
            new AddInsurancePolicyUseCase(repository).ExecuteAsync(
                pid, InsuranceLevel.Primary, 9, "POL2", null, null, sameDate, null, CancellationToken.None));
    }

    [Fact]
    public async Task Adding_a_policy_tolerates_an_unrelated_pre_existing_row_with_a_malformed_date()
    {
        // A row written before this service existed (or by legacy's own PHP) can hold a zero date --
        // GetPoliciesAsync must skip that row, not throw, so it never breaks an unrelated add for the
        // same patient.
        var pid = NextPid();
        await using var context = fixture.CreateDbContext();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO insurance_data (pid, type, provider, policy_number, date, date_end, uuid)
            VALUES ({pid}, 'tertiary', '7', 'LEGACY-ROW', '0000-00-00', NULL, {Guid.NewGuid().ToByteArray()})
            """);

        var repository = new EfInsurancePolicyRepository(context);

        var policy = await new AddInsurancePolicyUseCase(repository).ExecuteAsync(
            pid, InsuranceLevel.Primary, 7, "POL1", null, null, new DateOnly(2024, 1, 1), null, CancellationToken.None);

        Assert.Equal(InsuranceLevel.Primary, policy.Level);
    }
}
