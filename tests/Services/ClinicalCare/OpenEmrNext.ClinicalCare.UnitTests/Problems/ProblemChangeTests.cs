using OpenEmrNext.ClinicalCare.Domain.Problems;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.UnitTests.Problems;

/// <summary>
/// The rules of a problem update (M5c slice 3, ADR 0068) and of a problem's verification (PB-21), tested with no database or framework (DOM-19). A change shares
/// its field rules with recording, so most cases prove the same rule fires for an update.
/// </summary>
public class ProblemChangeTests
{
    private static readonly DateTime Now = new(2026, 11, 17, 9, 30, 0);

    private static readonly IssueCodeData Codes = new(
        new HashSet<int> { 0, 1, 2 }, new HashSet<int> { 0, 1, 4 },
        new HashSet<string> { "unconfirmed", "confirmed", "refuted", "entered-in-error" });

    private static ProblemChange Change(
        string? title = "Asthma", DateTime? begin = null, DateTime? end = null, string? diagnosis = "ICD10:J45.909", int outcome = 0, int occurrence = 1,
        string? referredBy = null, string? comments = "note", string? verification = "confirmed", IssueCodeData? codes = null) =>
        ProblemChange.Create(title, begin ?? new DateTime(2020, 5, 1), end, diagnosis, outcome, occurrence, referredBy, comments, verification, Now, codes ?? Codes);

    private static IReadOnlyList<string> ErrorsOf(Action act) => Assert.Throws<ClinicalCareValidationException>(act).Errors;

    [Fact]
    public void A_valid_change_keeps_every_editable_field()
    {
        var change = Change(
            title: " Asthma ", begin: new DateTime(2020, 5, 1), end: new DateTime(2021, 1, 2), diagnosis: "ICD10:J45.909", outcome: 1, occurrence: 4,
            referredBy: "Dr Smith", comments: "seasonal", verification: "confirmed");

        Assert.Equal("Asthma", change.Title);
        Assert.Equal(new DateTime(2020, 5, 1), change.BeginDate);
        Assert.Equal(new DateTime(2021, 1, 2), change.EndDate);
        Assert.Equal("ICD10:J45.909", change.Diagnosis);
        Assert.Equal(1, change.Outcome);
        Assert.Equal(4, change.Occurrence);
        Assert.Equal("Dr Smith", change.ReferredBy);
        Assert.Equal("seasonal", change.Comments);
        Assert.Equal("confirmed", change.Verification);
    }

    [Fact]
    public void Resolving_is_an_update_an_end_date_and_outcome_1_and_reopening_clears_the_end_date()
    {
        var resolved = Change(end: new DateTime(2022, 1, 1), outcome: 1);
        var reopened = Change(end: null, outcome: 0);

        Assert.Equal(new DateTime(2022, 1, 1), resolved.EndDate);
        Assert.Equal(1, resolved.Outcome);
        Assert.Null(reopened.EndDate);
    }

    [Fact]
    public void The_shared_field_rules_apply_to_an_update_as_to_a_record()
    {
        Assert.Contains("A title is required.", ErrorsOf(() => Change(title: " ")));
        Assert.Contains("A begin date is required.", ErrorsOf(() => ProblemChange.Create("x1", null, null, null, 0, 0, null, null, null, Now, Codes)));
        Assert.Contains("End date cannot be before the begin date.", ErrorsOf(() => Change(begin: new DateTime(2020, 5, 1), end: new DateTime(2020, 4, 1))));
        Assert.Contains("Outcome 9 is not a known outcome.", ErrorsOf(() => Change(outcome: 9)));
        Assert.Contains("Occurrence 2 is not a known occurrence.", ErrorsOf(() => Change(occurrence: 2)));
        Assert.Contains("Referred by must be at most 255 characters.", ErrorsOf(() => Change(referredBy: new string('x', 256))));
        Assert.Single(ErrorsOf(() => Change(diagnosis: "not-a-code")), e => e.StartsWith("Diagnosis must be", StringComparison.Ordinal));
    }

    [Fact]
    public void An_update_names_no_provider_so_there_is_no_provider_rule()
    {
        // The actor of an update is not a field (ADR 0069 #5): nothing in a change can be rejected for lacking one.
        Assert.DoesNotContain(ErrorsOfOrEmpty(() => Change()), e => e.Contains("ProviderId", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("unconfirmed")]
    [InlineData("entered-in-error")]
    public void PB_21_a_verification_on_the_condition_list_is_accepted(string verification) =>
        Assert.Equal(verification, Change(verification: verification).Verification);

    [Theory]
    [InlineData("differential")]
    [InlineData("CONFIRMED")]
    [InlineData("confirmed ")]
    public void PB_21_a_verification_not_on_the_list_is_rejected_and_the_match_is_exact(string verification) =>
        Assert.Contains($"Verification '{verification}' is not a known verification.", ErrorsOf(() => Change(verification: verification)));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void PB_21_a_blank_verification_means_none(string? blank) =>
        Assert.Null(Change(verification: blank).Verification);

    [Fact]
    public void PB_21_when_no_condition_verification_list_is_configured_any_given_code_is_rejected()
    {
        // Only the verification list is missing: outcomes and occurrences are configured, so nothing else can fail here.
        var none = new IssueCodeData(new HashSet<int> { 0, 1, 2 }, new HashSet<int> { 0, 1, 4 });

        Assert.Contains("Verification 'confirmed' is not a known verification.", ErrorsOf(() => Change(verification: "confirmed", codes: none)));
        Assert.Null(Change(verification: null, codes: none).Verification);
    }

    [Fact]
    public void PB_21_a_configured_code_longer_than_its_column_is_a_clean_message()
    {
        var long37 = new string('v', 37);
        var codes = new IssueCodeData(new HashSet<int> { 0 }, new HashSet<int> { 0 }, new HashSet<string> { long37 });

        Assert.Contains($"Verification '{long37}' is longer than the 36 characters a record can hold.", ErrorsOf(() => Change(verification: long37, codes: codes)));
    }

    [Fact]
    public void An_allergys_verification_list_is_not_a_problems()
    {
        // The allergy list holds the same words today, but they are different lists and the problem rule reads only its own.
        var allergyOnly = new IssueCodeData(new HashSet<int> { 0 }, new HashSet<int> { 0 });

        Assert.Contains("Verification 'confirmed' is not a known verification.", ErrorsOf(() => Change(verification: "confirmed", codes: allergyOnly)));
    }

    [Fact]
    public void Every_violation_is_reported_together()
    {
        var errors = ErrorsOf(() => ProblemChange.Create(" ", null, null, "bad", 9, 2, new string('x', 300), null, "zzz", Now, Codes));

        Assert.True(errors.Count >= 6, string.Join(" | ", errors));
    }

    [Fact]
    public void Dates_are_cut_to_whole_seconds_so_an_unchanged_date_with_a_stray_fraction_is_not_a_change()
    {
        var withFraction = new DateTime(2020, 5, 1, 8, 15, 30).AddTicks(7_654_321);

        var change = Change(begin: withFraction, end: withFraction.AddDays(1));

        Assert.Equal(new DateTime(2020, 5, 1, 8, 15, 30), change.BeginDate);
        Assert.Equal(new DateTime(2020, 5, 2, 8, 15, 30), change.EndDate);
    }

    [Fact]
    public void Blank_free_text_is_none()
    {
        var change = Change(referredBy: "  ", comments: "", diagnosis: " ");

        Assert.Null(change.ReferredBy);
        Assert.Null(change.Comments);
        Assert.Null(change.Diagnosis);
    }

    private static IReadOnlyList<string> ErrorsOfOrEmpty(Action act)
    {
        try { act(); return []; }
        catch (ClinicalCareValidationException ex) { return ex.Errors; }
    }
}
