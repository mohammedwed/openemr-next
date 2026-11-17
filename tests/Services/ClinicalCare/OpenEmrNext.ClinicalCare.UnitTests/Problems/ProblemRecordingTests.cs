using OpenEmrNext.ClinicalCare.Domain.Problems;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.UnitTests.Problems;

/// <summary>
/// Rules PB-1 to PB-9 (docs/clinical-care/08-record-problem-slice.md), tested with no database or framework (DOM-19). Rule ids are in the test names.
/// </summary>
public class ProblemRecordingTests
{
    private static readonly DateTime Now = new(2026, 11, 17, 9, 30, 0);

    // The snapshot's lists: outcome 0 to 5, occurrence 0, 1, 4, 5, 6, 7, 8.
    private static readonly IssueCodeData Codes = new(
        new HashSet<int> { 0, 1, 2, 3, 4, 5 }, new HashSet<int> { 0, 1, 4, 5, 6, 7, 8 });

    private static PatientReference Patient() => PatientReference.Create(Guid.NewGuid(), 42);

    private static ProblemRecording Create(
        string? title = "Asthma", DateTime? begin = null, DateTime? end = null, string? diagnosis = "ICD10:J45.909", int outcome = 0,
        int occurrence = 0, string? referredBy = null, string? comments = null, long providerId = 7, IssueCodeData? codes = null) =>
        ProblemRecording.Create(
            Patient(), title, begin ?? new DateTime(2020, 5, 1), end, diagnosis, outcome, occurrence, referredBy, comments, providerId, Now,
            codes ?? Codes);

    private static IReadOnlyList<string> ErrorsOf(Action act) => Assert.Throws<ClinicalCareValidationException>(act).Errors;

    [Fact]
    public void A_valid_recording_keeps_what_was_given()
    {
        var recording = Create(
            title: "Asthma", begin: new DateTime(2020, 5, 1), end: new DateTime(2021, 1, 2), diagnosis: "ICD10:J45.909;SNOMED-CT:195967001",
            outcome: 1, occurrence: 4, referredBy: "Dr Smith", comments: "Seasonal", providerId: 9);

        Assert.Equal("Asthma", recording.Title);
        Assert.Equal(new DateTime(2020, 5, 1), recording.BeginDate);
        Assert.Equal(new DateTime(2021, 1, 2), recording.EndDate);
        Assert.Equal("ICD10:J45.909;SNOMED-CT:195967001", recording.Diagnosis);
        Assert.Equal(1, recording.Outcome);
        Assert.Equal(4, recording.Occurrence);
        Assert.Equal("Dr Smith", recording.ReferredBy);
        Assert.Equal("Seasonal", recording.Comments);
        Assert.Equal(9, recording.ProviderId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PB_1_a_title_is_required(string? title) =>
        Assert.Contains("A title is required.", ErrorsOf(() => Create(title: title)));

    [Theory]
    [InlineData("a")]
    [InlineData(" b ")]
    public void PB_1_a_title_is_at_least_two_characters_after_trimming(string title) =>
        Assert.Contains("Title must be between 2 and 255 characters.", ErrorsOf(() => Create(title: title)));

    [Fact]
    public void PB_1_a_title_is_at_most_255_characters() =>
        Assert.Contains("Title must be between 2 and 255 characters.", ErrorsOf(() => Create(title: new string('x', 256))));

    [Fact]
    public void PB_1_the_title_is_trimmed_and_a_255_character_title_is_kept()
    {
        Assert.Equal("Asthma", Create(title: "  Asthma  ").Title);
        Assert.Equal(255, Create(title: new string('x', 255)).Title.Length);
    }

    [Fact]
    public void PB_2_a_begin_date_is_required() =>
        Assert.Contains("A begin date is required.", ErrorsOf(() =>
            ProblemRecording.Create(Patient(), "Asthma", null, null, null, 0, 0, null, null, 7, Now, Codes)));

    [Fact]
    public void PB_2_a_begin_date_must_be_plausible()
    {
        Assert.Contains("Begin date must be between 1900-01-01 and one year from now.", ErrorsOf(() => Create(begin: new DateTime(1, 1, 1))));
        Assert.Contains("Begin date must be between 1900-01-01 and one year from now.", ErrorsOf(() => Create(begin: Now.AddDays(400))));
    }

    [Fact]
    public void PB_3_an_end_date_cannot_be_before_the_begin_date() =>
        Assert.Contains("End date cannot be before the begin date.",
            ErrorsOf(() => Create(begin: new DateTime(2020, 5, 1), end: new DateTime(2020, 4, 30))));

    [Fact]
    public void PB_3_an_end_date_on_the_begin_date_is_fine_and_so_is_none()
    {
        Assert.Equal(new DateTime(2020, 5, 1), Create(begin: new DateTime(2020, 5, 1), end: new DateTime(2020, 5, 1)).EndDate);
        Assert.Null(Create(end: null).EndDate);
    }

    [Fact]
    public void PB_3_an_end_date_must_be_plausible() =>
        Assert.Contains("End date must be between 1900-01-01 and one year from now.", ErrorsOf(() => Create(end: Now.AddDays(400))));

    [Theory]
    [InlineData("ICD10:J45.909")]
    [InlineData("SNOMED-CT:195967001")]
    [InlineData("ICD10:J45.909;SNOMED-CT:195967001")]
    [InlineData("ICD10:J45.909;ICD10:J45.901;RXCUI:12345")]
    public void PB_4_a_diagnosis_of_code_pairs_is_accepted(string diagnosis) =>
        Assert.Equal(diagnosis, Create(diagnosis: diagnosis).Diagnosis);

    [Theory]
    [InlineData("J45.909")]                  // no system
    [InlineData("ICD10:")]                   // no code
    [InlineData(":J45")]                     // no system
    [InlineData("ICD10:J45;")]               // trailing separator
    [InlineData("ICD10:J45;;SNOMED:1")]      // empty pair
    [InlineData("ICD 10:J45")]               // space in the system
    public void PB_4_a_diagnosis_that_is_not_code_pairs_is_rejected(string diagnosis) =>
        Assert.Single(ErrorsOf(() => Create(diagnosis: diagnosis)), e => e.StartsWith("Diagnosis must be", StringComparison.Ordinal));

    [Fact]
    public void PB_4_a_diagnosis_is_2_to_255_characters()
    {
        Assert.Contains("Diagnosis must be between 2 and 255 characters.", ErrorsOf(() => Create(diagnosis: "x")));
        Assert.Contains("Diagnosis must be between 2 and 255 characters.", ErrorsOf(() => Create(diagnosis: "ICD10:" + new string('9', 250))));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PB_4_a_blank_diagnosis_is_none(string? blank) =>
        Assert.Null(Create(diagnosis: blank).Diagnosis);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public void PB_5_an_outcome_on_the_list_is_accepted(int outcome) =>
        Assert.Equal(outcome, Create(outcome: outcome).Outcome);

    [Theory]
    [InlineData(9)]
    [InlineData(-1)]
    public void PB_5_an_outcome_not_on_the_list_is_rejected(int outcome) =>
        Assert.Contains($"Outcome {outcome} is not a known outcome.", ErrorsOf(() => Create(outcome: outcome)));

    [Fact]
    public void PB_5_zero_is_always_allowed_even_when_no_list_is_configured() =>
        Assert.Equal(0, Create(outcome: 0, occurrence: 0, codes: new IssueCodeData(new HashSet<int>(), new HashSet<int>())).Outcome);

    [Fact]
    public void PB_5_any_other_code_is_rejected_when_no_list_is_configured() =>
        Assert.Contains("Outcome 1 is not a known outcome.", ErrorsOf(() => Create(outcome: 1, codes: new IssueCodeData(new HashSet<int>(), new HashSet<int>()))));

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(8)]
    public void PB_6_an_occurrence_on_the_list_is_accepted(int occurrence) =>
        Assert.Equal(occurrence, Create(occurrence: occurrence).Occurrence);

    [Fact]
    public void PB_6_an_occurrence_not_on_the_list_is_rejected() =>
        Assert.Contains("Occurrence 2 is not a known occurrence.", ErrorsOf(() => Create(occurrence: 2)));   // 2 is an outcome, not an occurrence

    [Fact]
    public void PB_7_and_PB_8_free_text_is_bounded()
    {
        Assert.Contains("Referred by must be at most 255 characters.", ErrorsOf(() => Create(referredBy: new string('x', 256))));
        Assert.Contains($"Comments must be at most {ProblemRecording.MaxCommentsLength} characters.",
            ErrorsOf(() => Create(comments: new string('x', ProblemRecording.MaxCommentsLength + 1))));
    }

    [Fact]
    public void PB_7_and_PB_8_blank_free_text_is_none()
    {
        var recording = Create(referredBy: "  ", comments: "");

        Assert.Null(recording.ReferredBy);
        Assert.Null(recording.Comments);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData((long)int.MaxValue + 1)]
    public void PB_9_the_provider_must_fit_the_column(long providerId) =>
        Assert.Contains("ProviderId must identify the acting provider.", ErrorsOf(() => Create(providerId: providerId)));

    [Fact]
    public void Every_violation_is_reported_together()
    {
        var errors = ErrorsOf(() =>
            ProblemRecording.Create(Patient(), " ", null, null, "bad", 9, 2, new string('x', 300), null, 0, Now, Codes));

        Assert.True(errors.Count >= 6, string.Join(" | ", errors));
    }

    [Fact]
    public void Dates_are_cut_to_whole_seconds()
    {
        var withFraction = new DateTime(2020, 5, 1, 8, 15, 30).AddTicks(7_654_321);

        var recording = Create(begin: withFraction, end: withFraction.AddDays(1));

        Assert.Equal(new DateTime(2020, 5, 1, 8, 15, 30), recording.BeginDate);
        Assert.Equal(new DateTime(2020, 5, 2, 8, 15, 30), recording.EndDate);
    }
}
