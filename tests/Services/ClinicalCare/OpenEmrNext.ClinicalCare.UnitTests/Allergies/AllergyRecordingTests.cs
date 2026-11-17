using OpenEmrNext.ClinicalCare.Domain.Allergies;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.UnitTests.Allergies;

/// <summary>
/// Rules AL-1 to AL-9 (docs/clinical-care/09-record-allergy-slice.md), tested with no database or framework (DOM-19). Title, date and diagnosis rules are
/// shared with problems, so the cases here that repeat a problem rule prove the same rule fires; the rest are what an allergy does differently.
/// </summary>
public class AllergyRecordingTests
{
    private static readonly DateTime Now = new(2026, 11, 17, 9, 30, 0);

    private static readonly AllergyCodeData Codes = new(
        new HashSet<string> { "unassigned", "hives", "nausea", "shortness_of_breath" },
        new HashSet<string> { "unassigned", "mild", "moderate", "severe", "life_threatening_severity" },
        new HashSet<string> { "unconfirmed", "confirmed", "refuted", "entered-in-error" });

    private static PatientReference Patient() => PatientReference.Create(Guid.NewGuid(), 42);

    private static AllergyRecording Create(
        string? title = "Penicillin", DateTime? begin = null, DateTime? end = null, string? diagnosis = null, string? reaction = "hives",
        string? severity = "severe", string? verification = "confirmed", string? comments = null, long providerId = 7, AllergyCodeData? codes = null) =>
        AllergyRecording.Create(Patient(), title, begin, end, diagnosis, reaction, severity, verification, comments, providerId, Now, codes ?? Codes);

    private static IReadOnlyList<string> ErrorsOf(Action act) => Assert.Throws<ClinicalCareValidationException>(act).Errors;

    [Fact]
    public void A_valid_recording_keeps_what_was_given()
    {
        var recording = Create(
            begin: new DateTime(2015, 3, 2), end: new DateTime(2020, 1, 1), diagnosis: "SNOMED-CT:91936005", comments: "anaphylaxis", providerId: 9);

        Assert.Equal("Penicillin", recording.Title);
        Assert.Equal(new DateTime(2015, 3, 2), recording.BeginDate);
        Assert.Equal(new DateTime(2020, 1, 1), recording.EndDate);
        Assert.Equal("SNOMED-CT:91936005", recording.Diagnosis);
        Assert.Equal("hives", recording.Reaction);
        Assert.Equal("severe", recording.Severity);
        Assert.Equal("confirmed", recording.Verification);
        Assert.Equal("anaphylaxis", recording.Comments);
        Assert.Equal(9, recording.ProviderId);
    }

    [Fact]
    public void AL_2_a_begin_date_is_optional_unlike_a_problems()
    {
        var recording = Create(begin: null);

        Assert.Null(recording.BeginDate);
    }

    [Fact]
    public void AL_2_an_end_date_without_a_begin_date_is_accepted_there_is_nothing_to_compare_it_with() =>
        Assert.Equal(new DateTime(2020, 1, 1), Create(begin: null, end: new DateTime(2020, 1, 1)).EndDate);

    [Fact]
    public void AL_2_dates_that_are_given_obey_the_same_range_and_order_as_a_problems()
    {
        Assert.Contains("Begin date must be between 1900-01-01 and one year from now.", ErrorsOf(() => Create(begin: new DateTime(1, 1, 1))));
        Assert.Contains("End date cannot be before the begin date.", ErrorsOf(() => Create(begin: new DateTime(2020, 5, 1), end: new DateTime(2020, 4, 1))));
        Assert.Contains("End date must be between 1900-01-01 and one year from now.", ErrorsOf(() => Create(end: Now.AddDays(400))));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void AL_1_a_title_is_required(string? title) =>
        Assert.Contains("A title is required.", ErrorsOf(() => Create(title: title)));

    [Theory]
    [InlineData("a")]
    public void AL_1_a_title_is_at_least_two_characters(string title) =>
        Assert.Contains("Title must be between 2 and 255 characters.", ErrorsOf(() => Create(title: title)));

    [Fact]
    public void AL_1_a_title_is_at_most_255_characters_and_is_trimmed()
    {
        Assert.Contains("Title must be between 2 and 255 characters.", ErrorsOf(() => Create(title: new string('x', 256))));
        Assert.Equal("Penicillin", Create(title: "  Penicillin  ").Title);
    }

    [Theory]
    [InlineData("SNOMED-CT:91936005")]
    [InlineData("SNOMED-CT:91936005;RXCUI:7980")]
    public void AL_3_a_diagnosis_of_code_pairs_is_accepted(string diagnosis) =>
        Assert.Equal(diagnosis, Create(diagnosis: diagnosis).Diagnosis);

    [Theory]
    [InlineData("91936005")]
    [InlineData("SNOMED-CT:")]
    [InlineData("A:1;")]
    public void AL_3_a_diagnosis_that_is_not_code_pairs_is_rejected(string diagnosis) =>
        Assert.Single(ErrorsOf(() => Create(diagnosis: diagnosis)), e => e.StartsWith("Diagnosis must be", StringComparison.Ordinal));

    [Theory]
    [InlineData("hives")]
    [InlineData("nausea")]
    [InlineData("unassigned")]
    public void AL_4_a_reaction_on_the_list_is_accepted(string reaction) =>
        Assert.Equal(reaction, Create(reaction: reaction).Reaction);

    [Theory]
    [InlineData("rash")]
    [InlineData("HIVES")]      // option ids compare exactly, as list_options.option_id does
    [InlineData("hives ")]
    public void AL_4_a_reaction_not_on_the_list_is_rejected_and_the_match_is_exact(string reaction) =>
        Assert.Contains($"Reaction '{reaction}' is not a known reaction.", ErrorsOf(() => Create(reaction: reaction)));

    [Fact]
    public void AL_5_a_severity_not_on_the_list_is_rejected() =>
        Assert.Contains("Severity 'catastrophic' is not a known severity.", ErrorsOf(() => Create(severity: "catastrophic")));

    [Fact]
    public void AL_6_a_verification_not_on_the_list_is_rejected_and_a_problems_verification_list_is_not_this_one() =>
        Assert.Contains("Verification 'differential' is not a known verification.", ErrorsOf(() => Create(verification: "differential")));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AL_4_to_AL_6_a_blank_code_means_none(string? blank)
    {
        var recording = Create(reaction: blank, severity: blank, verification: blank);

        Assert.Null(recording.Reaction);
        Assert.Null(recording.Severity);
        Assert.Null(recording.Verification);
    }

    [Fact]
    public void AL_4_to_AL_6_when_no_list_is_configured_any_given_code_is_rejected_and_blank_is_still_fine()
    {
        var none = new AllergyCodeData(new HashSet<string>(), new HashSet<string>(), new HashSet<string>());

        Assert.Contains("Reaction 'hives' is not a known reaction.", ErrorsOf(() => Create(reaction: "hives", severity: null, verification: null, codes: none)));
        Assert.Null(Create(reaction: null, severity: null, verification: null, codes: none).Reaction);
    }

    [Fact]
    public void AL_4_to_AL_6_a_configured_code_longer_than_its_column_is_a_clean_message_not_a_database_error()
    {
        // A site can configure an option id longer than the column that will hold it; that must never reach the INSERT (review findings R-1 to R-7).
        var longSeverity = new string('s', 51);
        var longVerification = new string('v', 37);
        var codes = new AllergyCodeData(
            new HashSet<string> { new string('r', 256) }, new HashSet<string> { longSeverity }, new HashSet<string> { longVerification });

        var errors = ErrorsOf(() => Create(reaction: new string('r', 256), severity: longSeverity, verification: longVerification, codes: codes));

        Assert.Equal(3, errors.Count);
        Assert.All(errors, e => Assert.Contains("longer than the", e));
    }

    [Fact]
    public void AL_7_comments_are_bounded_and_blank_comments_are_none()
    {
        Assert.Contains($"Comments must be at most {AllergyRecordingLimits.Comments} characters.",
            ErrorsOf(() => Create(comments: new string('x', AllergyRecordingLimits.Comments + 1))));
        Assert.Null(Create(comments: "  ").Comments);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData((long)int.MaxValue + 1)]
    public void AL_8_the_provider_must_fit_the_column(long providerId) =>
        Assert.Contains("ProviderId must identify the acting provider.", ErrorsOf(() => Create(providerId: providerId)));

    [Fact]
    public void Every_violation_is_reported_together()
    {
        var errors = ErrorsOf(() => AllergyRecording.Create(
            Patient(), " ", new DateTime(1, 1, 1), null, "bad", "x", "y", "z", null, 0, Now, Codes));

        Assert.True(errors.Count >= 7, string.Join(" | ", errors));
    }

    [Fact]
    public void Dates_are_cut_to_whole_seconds()
    {
        var withFraction = new DateTime(2015, 3, 2, 8, 15, 30).AddTicks(7_654_321);

        var recording = Create(begin: withFraction, end: withFraction.AddDays(1));

        Assert.Equal(new DateTime(2015, 3, 2, 8, 15, 30), recording.BeginDate);
        Assert.Equal(new DateTime(2015, 3, 3, 8, 15, 30), recording.EndDate);
    }

    /// <summary>Shared constants live in an internal class; the public ones the problem rules already expose are the same numbers.</summary>
    private static class AllergyRecordingLimits
    {
        public const int Comments = OpenEmrNext.ClinicalCare.Domain.Problems.ProblemRecording.MaxCommentsLength;
    }
}
