using OpenEmrNext.ClinicalCare.Domain.Allergies;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.UnitTests.Allergies;

/// <summary>The rules of an allergy update (M5c slice 3, ADR 0068), tested with no database or framework (DOM-19). It shares its field rules with recording.</summary>
public class AllergyChangeTests
{
    private static readonly DateTime Now = new(2026, 11, 17, 9, 30, 0);

    private static readonly AllergyCodeData Codes = new(
        new HashSet<string> { "hives", "nausea" }, new HashSet<string> { "mild", "severe" }, new HashSet<string> { "unconfirmed", "confirmed" });

    private static AllergyChange Change(
        string? title = "Penicillin", DateTime? begin = null, DateTime? end = null, string? diagnosis = null, string? reaction = "hives", string? severity = "severe",
        string? verification = "confirmed", string? comments = "note") =>
        AllergyChange.Create(title, begin, end, diagnosis, reaction, severity, verification, comments, Now, Codes);

    private static IReadOnlyList<string> ErrorsOf(Action act) => Assert.Throws<ClinicalCareValidationException>(act).Errors;

    [Fact]
    public void A_valid_change_keeps_every_editable_field_and_a_begin_date_is_optional()
    {
        var change = Change(begin: null, end: new DateTime(2022, 1, 1), diagnosis: "SNOMED-CT:91936005");

        Assert.Equal("Penicillin", change.Title);
        Assert.Null(change.BeginDate);
        Assert.Equal(new DateTime(2022, 1, 1), change.EndDate);
        Assert.Equal("SNOMED-CT:91936005", change.Diagnosis);
        Assert.Equal("hives", change.Reaction);
        Assert.Equal("severe", change.Severity);
        Assert.Equal("confirmed", change.Verification);
        Assert.Equal("note", change.Comments);
    }

    [Fact]
    public void Ending_an_allergy_is_an_update_and_reopening_clears_the_end_date()
    {
        Assert.Equal(new DateTime(2022, 1, 1), Change(end: new DateTime(2022, 1, 1)).EndDate);
        Assert.Null(Change(end: null).EndDate);
    }

    [Fact]
    public void The_shared_field_rules_apply_to_an_update_as_to_a_record()
    {
        Assert.Contains("A title is required.", ErrorsOf(() => Change(title: null)));
        Assert.Contains("End date cannot be before the begin date.", ErrorsOf(() => Change(begin: new DateTime(2020, 5, 1), end: new DateTime(2020, 4, 1))));
        Assert.Contains("Reaction 'rash' is not a known reaction.", ErrorsOf(() => Change(reaction: "rash")));
        Assert.Contains("Severity 'catastrophic' is not a known severity.", ErrorsOf(() => Change(severity: "catastrophic")));
        Assert.Contains("Verification 'differential' is not a known verification.", ErrorsOf(() => Change(verification: "differential")));
        Assert.Single(ErrorsOf(() => Change(diagnosis: "91936005")), e => e.StartsWith("Diagnosis must be", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Blank_codes_mean_none(string? blank)
    {
        var change = Change(reaction: blank, severity: blank, verification: blank);

        Assert.Null(change.Reaction);
        Assert.Null(change.Severity);
        Assert.Null(change.Verification);
    }

    [Fact]
    public void Dates_are_cut_to_whole_seconds()
    {
        var withFraction = new DateTime(2015, 3, 2, 8, 15, 30).AddTicks(7_654_321);

        Assert.Equal(new DateTime(2015, 3, 2, 8, 15, 30), Change(begin: withFraction).BeginDate);
    }

    [Fact]
    public void Every_violation_is_reported_together()
    {
        var errors = ErrorsOf(() => AllergyChange.Create(" ", new DateTime(1, 1, 1), null, "bad", "x", "y", "z", null, Now, Codes));

        Assert.True(errors.Count >= 6, string.Join(" | ", errors));
    }
}
