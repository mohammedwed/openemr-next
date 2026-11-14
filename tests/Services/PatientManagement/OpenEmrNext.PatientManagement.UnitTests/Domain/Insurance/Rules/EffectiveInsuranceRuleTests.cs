using OpenEmrNext.PatientManagement.Domain.Insurance;
using OpenEmrNext.PatientManagement.Domain.Insurance.ValueObjects;
using OpenEmrNext.PatientManagement.Domain.Insurance.Rules;
using OpenEmrNext.PatientManagement.Domain.Insurance.ReadModels;

namespace OpenEmrNext.PatientManagement.UnitTests.Domain.Insurance.Rules;

public class EffectiveInsuranceRuleTests
{
    private static readonly DateOnly Today = new(2026, 11, 13);

    private static InsurancePolicyRow Row(
        long id, InsuranceLevel level, string provider = "1", string date = "", string dateEnd = "") =>
        new(id, Uuid: null, level.ToWireValue(), provider, date, dateEnd);

    [Fact]
    public void An_always_open_policy_is_in_force_with_no_start_or_end()
    {
        var result = EffectiveInsuranceRule.Choose([Row(1, InsuranceLevel.Primary)], Today);

        var policy = Assert.Single(result);
        Assert.Equal(InsuranceLevel.Primary, policy.Level);
        Assert.Null(policy.Start);
        Assert.Null(policy.End);
        Assert.Equal(1, policy.CompanyId);
    }

    [Fact]
    public void A_blank_start_loses_to_any_stored_start_zero_date_included()
    {
        var blank = Row(1, InsuranceLevel.Primary, date: "");
        var zeroDate = Row(2, InsuranceLevel.Primary, date: "0000-00-00");

        var result = EffectiveInsuranceRule.Choose([blank, zeroDate], Today);

        Assert.Equal(2, Assert.Single(result).Id);
    }

    [Fact]
    public void Between_two_blank_starts_the_higher_id_wins()
    {
        var older = Row(1, InsuranceLevel.Primary);
        var newer = Row(2, InsuranceLevel.Primary);

        var result = EffectiveInsuranceRule.Choose([older, newer], Today);

        Assert.Equal(2, Assert.Single(result).Id);
    }

    [Fact]
    public void A_later_real_start_outranks_an_earlier_one()
    {
        var early = Row(1, InsuranceLevel.Primary, date: "2020-01-01");
        var later = Row(2, InsuranceLevel.Primary, date: "2025-01-01");

        var result = EffectiveInsuranceRule.Choose([early, later], Today);

        Assert.Equal(2, Assert.Single(result).Id);
    }

    [Fact]
    public void An_end_of_zero_date_means_open_not_expired()
    {
        var row = Row(1, InsuranceLevel.Primary, date: "2000-01-01", dateEnd: "0000-00-00");

        var result = EffectiveInsuranceRule.Choose([row], Today);

        Assert.Null(Assert.Single(result).End);
    }

    [Theory]
    [InlineData("2026-11-13")] // on the start day
    [InlineData("2026-11-14")] // after the start day, still open-ended
    public void A_policy_is_in_force_on_and_after_its_start(string onDayString)
    {
        var row = Row(1, InsuranceLevel.Primary, date: "2026-11-13");
        var onDay = DateOnly.Parse(onDayString);

        var result = EffectiveInsuranceRule.Choose([row], onDay);

        Assert.Single(result);
    }

    [Fact]
    public void A_policy_is_not_in_force_before_its_start()
    {
        var row = Row(1, InsuranceLevel.Primary, date: "2026-11-14");

        var result = EffectiveInsuranceRule.Choose([row], new DateOnly(2026, 11, 13));

        Assert.Empty(result);
    }

    [Fact]
    public void A_policy_is_not_in_force_after_its_end()
    {
        var row = Row(1, InsuranceLevel.Primary, dateEnd: "2026-11-12");

        var result = EffectiveInsuranceRule.Choose([row], new DateOnly(2026, 11, 13));

        Assert.Empty(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("not-a-number")]
    public void A_winning_policy_with_no_company_has_no_coverage(string provider)
    {
        var row = Row(1, InsuranceLevel.Primary, provider: provider);

        var result = EffectiveInsuranceRule.Choose([row], Today);

        Assert.Empty(result);
    }

    [Fact]
    public void Levels_are_independent_an_ended_primary_does_not_hide_a_current_secondary()
    {
        var endedPrimary = Row(1, InsuranceLevel.Primary, dateEnd: "2020-01-01");
        var currentSecondary = Row(2, InsuranceLevel.Secondary);

        var result = EffectiveInsuranceRule.Choose([endedPrimary, currentSecondary], Today);

        var policy = Assert.Single(result);
        Assert.Equal(InsuranceLevel.Secondary, policy.Level);
    }

    [Fact]
    public void Results_are_ordered_primary_first_regardless_of_input_order()
    {
        var tertiary = Row(1, InsuranceLevel.Tertiary);
        var primary = Row(2, InsuranceLevel.Primary);
        var secondary = Row(3, InsuranceLevel.Secondary);

        var result = EffectiveInsuranceRule.Choose([tertiary, primary, secondary], Today);

        Assert.Equal([InsuranceLevel.Primary, InsuranceLevel.Secondary, InsuranceLevel.Tertiary], result.Select(p => p.Level));
    }

    [Fact]
    public void A_partial_date_with_a_zero_day_starts_on_the_first_of_that_month()
    {
        var row = Row(1, InsuranceLevel.Primary, date: "2026-05-00");

        var result = EffectiveInsuranceRule.Choose([row], new DateOnly(2026, 5, 1));

        Assert.Equal(new DateOnly(2026, 5, 1), Assert.Single(result).Start);
    }

    [Fact]
    public void A_partial_date_with_a_zero_month_starts_on_january_first()
    {
        var row = Row(1, InsuranceLevel.Primary, date: "2026-00-00");

        var result = EffectiveInsuranceRule.Choose([row], new DateOnly(2026, 1, 1));

        Assert.Equal(new DateOnly(2026, 1, 1), Assert.Single(result).Start);
    }

    [Fact]
    public void A_partial_end_date_with_a_zero_day_expires_at_the_end_of_the_previous_month()
    {
        var row = Row(1, InsuranceLevel.Primary, dateEnd: "2026-05-00");

        var result = EffectiveInsuranceRule.Choose([row], new DateOnly(2026, 4, 30));

        // "a zero day, 2026-05-00, gives 2026-05-01" as the first day after -- so the last day in
        // force is 2026-04-30, one day before that.
        Assert.Equal(new DateOnly(2026, 4, 30), Assert.Single(result).End);
    }

    [Theory]
    [InlineData(" 007 ", 7L)]
    [InlineData("0", null)]
    [InlineData("", null)]
    [InlineData("not-a-number", null)]
    [InlineData("42", 42L)]
    public void StoredId_matches_legacy_exactly(string value, long? expected) =>
        Assert.Equal(expected, EffectiveInsuranceRule.StoredId(value));
}
