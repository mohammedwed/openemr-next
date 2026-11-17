using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Domain.Allergies;
using OpenEmrNext.ClinicalCare.Domain.Problems;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.UnitTests.Problems;

/// <summary>"Did anything change?" has one answer for problems and allergies, shared by the event, the audit row and the decision to write (ADR 0068).</summary>
public class IssueChangeSetTests
{
    private static readonly DateTime Now = new(2026, 11, 17, 9, 30, 0);
    private static readonly DateTime Begin = new(2020, 5, 1);
    private static readonly IssueCodeData ProblemCodes = new(
        new HashSet<int> { 0, 1, 2 }, new HashSet<int> { 0, 1, 4 }, new HashSet<string> { "confirmed", "refuted" });
    private static readonly AllergyCodeData AllergyCodes = new(
        new HashSet<string> { "hives", "nausea" }, new HashSet<string> { "mild", "severe" }, new HashSet<string> { "confirmed", "refuted" });

    private static ProblemView StoredProblem() =>
        new(Guid.NewGuid(), Guid.NewGuid(), 42, "Asthma", Begin, new DateTime(2021, 1, 1), "ICD10:J45.909", 1, 4, "Dr Smith", "note", null, null, null, "confirmed");

    private static ProblemChange P(
        string title = "Asthma", DateTime? begin = null, DateTime? end = null, string? diagnosis = "ICD10:J45.909", int outcome = 1, int occurrence = 4,
        string? referredBy = "Dr Smith", string? comments = "note", string? verification = "confirmed") =>
        ProblemChange.Create(title, begin ?? Begin, end ?? new DateTime(2021, 1, 1), diagnosis, outcome, occurrence, referredBy, comments, verification, Now, ProblemCodes);

    private static AllergyView StoredAllergy() =>
        new(Guid.NewGuid(), Guid.NewGuid(), 42, "Penicillin", Begin, null, "SNOMED-CT:91936005", "hives", "severe", "confirmed", "note", null, null, null);

    private static AllergyChange A(
        string title = "Penicillin", DateTime? begin = null, DateTime? end = null, string? diagnosis = "SNOMED-CT:91936005", string? reaction = "hives",
        string? severity = "severe", string? verification = "confirmed", string? comments = "note") =>
        AllergyChange.Create(title, begin ?? Begin, end, diagnosis, reaction, severity, verification, comments, Now, AllergyCodes);

    [Fact]
    public void An_identical_change_changes_nothing()
    {
        Assert.Empty(ProblemChangeSet.Between(StoredProblem(), P()));
        Assert.Empty(AllergyChangeSet.Between(StoredAllergy(), A()));
    }

    [Theory]
    [InlineData("title")]
    [InlineData("beginDate")]
    [InlineData("endDate")]
    [InlineData("diagnosis")]
    [InlineData("outcome")]
    [InlineData("occurrence")]
    [InlineData("referredBy")]
    [InlineData("comments")]
    [InlineData("verification")]
    public void Changing_exactly_one_problem_field_reports_exactly_that_field(string field)
    {
        var change = field switch
        {
            "title" => P(title: "Asthma, severe"),
            "beginDate" => P(begin: Begin.AddDays(-1)),
            "endDate" => P(end: new DateTime(2022, 1, 1)),
            "diagnosis" => P(diagnosis: "ICD10:J45.901"),
            "outcome" => P(outcome: 2),
            "occurrence" => P(occurrence: 1),
            "referredBy" => P(referredBy: "Dr Jones"),
            "comments" => P(comments: "changed"),
            _ => P(verification: "refuted")
        };

        Assert.Equal([field], ProblemChangeSet.Between(StoredProblem(), change));
    }

    [Theory]
    [InlineData("title")]
    [InlineData("beginDate")]
    [InlineData("endDate")]
    [InlineData("diagnosis")]
    [InlineData("reaction")]
    [InlineData("severity")]
    [InlineData("verification")]
    [InlineData("comments")]
    public void Changing_exactly_one_allergy_field_reports_exactly_that_field(string field)
    {
        var change = field switch
        {
            "title" => A(title: "Amoxicillin"),
            "beginDate" => A(begin: Begin.AddDays(-1)),
            "endDate" => A(end: new DateTime(2022, 1, 1)),
            "diagnosis" => A(diagnosis: "SNOMED-CT:1"),
            "reaction" => A(reaction: "nausea"),
            "severity" => A(severity: "mild"),
            "verification" => A(verification: "refuted"),
            _ => A(comments: "changed")
        };

        Assert.Equal([field], AllergyChangeSet.Between(StoredAllergy(), change));
    }

    [Fact]
    public void Resolving_a_problem_reports_the_end_date_and_the_outcome_and_nothing_else()
    {
        var open = StoredProblem() with { EndDate = null, Outcome = 0 };

        Assert.Equal(["endDate", "outcome"], ProblemChangeSet.Between(open, P(end: new DateTime(2022, 1, 1), outcome: 1)));
    }

    [Fact]
    public void A_null_and_an_empty_text_are_the_same_value()
    {
        Assert.Empty(ProblemChangeSet.Between(StoredProblem() with { Comments = null, ReferredBy = "", Diagnosis = null, Verification = "" }, P(comments: "", referredBy: null, diagnosis: null, verification: null)));
        Assert.Empty(AllergyChangeSet.Between(StoredAllergy() with { Reaction = null, Verification = "", Comments = "" }, A(reaction: null, verification: null, comments: null)));
    }

    [Fact]
    public void Clearing_a_text_is_a_change()
    {
        Assert.Equal(["comments"], ProblemChangeSet.Between(StoredProblem(), P(comments: null)));
        Assert.Equal(["reaction"], AllergyChangeSet.Between(StoredAllergy(), A(reaction: null)));
    }

    [Fact]
    public void A_stored_row_with_no_title_or_dates_differs_from_a_change_that_has_them()
    {
        var bare = StoredProblem() with { Title = null, BeginDate = null };

        Assert.Equal(["title", "beginDate"], ProblemChangeSet.Between(bare, P()).Take(2));
    }
}
