using OpenEmrNext.ClinicalCare.Domain.IssueLinks;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.UnitTests.IssueLinks;

/// <summary>Rules IE-1 to IE-3 (docs/clinical-care/11-link-issue-to-encounter-slice.md), tested with no database or framework (DOM-19). Rule ids are in the test names.</summary>
public class EncounterIssueLinkTests
{
    private static readonly Guid EncounterUuid = Guid.Parse("e0e0e0e0-0000-4000-8000-000000000001");
    private static readonly Guid PatientUuid = Guid.Parse("b1a9c5f0-0000-4000-8000-0000000000aa");

    private static IssueReference Issue(long pid = 42, IssueKind kind = IssueKind.Problem) => new(Guid.NewGuid(), 17, kind, pid, "Asthma");

    private static EncounterIssueLink Create(
        long? number = 1001, Guid? patientUuid = null, long? patientPid = 42, IssueReference? issue = null, bool resolved = false) =>
        EncounterIssueLink.Create(
            EncounterUuid, number, patientUuid ?? PatientUuid, patientPid, issue ?? Issue(), resolved, EncounterIssueLinkIdentity.Create(Guid.NewGuid()));

    private static IReadOnlyList<string> ErrorsOf(Action act) => Assert.Throws<ClinicalCareValidationException>(act).Errors;

    [Fact]
    public void A_link_between_an_encounter_and_an_issue_of_the_same_patient_keeps_everything()
    {
        var issue = Issue(kind: IssueKind.Allergy);

        var link = Create(issue: issue, resolved: true);

        Assert.Equal(EncounterUuid, link.EncounterUuid);
        Assert.Equal(1001, link.EncounterNumber);
        Assert.Equal(PatientUuid, link.Patient.Uuid);
        Assert.Equal(42, link.Patient.Pid);
        Assert.Same(issue, link.Issue);
        Assert.True(link.Resolved);
    }

    [Fact]
    public void IE_3_an_issue_of_a_different_patient_is_refused_which_legacy_does_not_check()
    {
        var errors = ErrorsOf(() => Create(issue: Issue(pid: 99)));

        Assert.Equal(["The issue belongs to a different patient than the encounter."], errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    [InlineData(-5L)]
    public void IE_1_an_encounter_with_no_number_cannot_be_linked_to(long? number) =>
        Assert.Contains("The encounter has no encounter number, so an issue cannot be linked to it.", ErrorsOf(() => Create(number: number)));

    [Fact]
    public void IE_2_an_encounter_whose_patient_is_unknown_cannot_be_linked_to()
    {
        Assert.Contains("The encounter's patient is unknown, so an issue cannot be linked to it.", ErrorsOf(() => EncounterIssueLink.Create(
            EncounterUuid, 1001, null, 42, Issue(), false, EncounterIssueLinkIdentity.Create(Guid.NewGuid()))));
        Assert.Contains("The encounter's patient is unknown, so an issue cannot be linked to it.", ErrorsOf(() => EncounterIssueLink.Create(
            EncounterUuid, 1001, PatientUuid, null, Issue(), false, EncounterIssueLinkIdentity.Create(Guid.NewGuid()))));
        Assert.Contains("The encounter's patient is unknown, so an issue cannot be linked to it.", ErrorsOf(() => EncounterIssueLink.Create(
            EncounterUuid, 1001, Guid.Empty, 42, Issue(), false, EncounterIssueLinkIdentity.Create(Guid.NewGuid()))));
    }

    [Fact]
    public void A_missing_number_and_an_unknown_patient_are_reported_together_and_the_patient_comparison_is_skipped_when_the_patient_is_unknown()
    {
        var errors = ErrorsOf(() => EncounterIssueLink.Create(
            EncounterUuid, null, null, null, Issue(pid: 99), false, EncounterIssueLinkIdentity.Create(Guid.NewGuid())));

        Assert.Equal(2, errors.Count);
        Assert.DoesNotContain(errors, e => e.Contains("different patient", StringComparison.Ordinal));
    }

    [Fact]
    public void The_database_id_is_zero_until_assigned_and_can_be_assigned_once()
    {
        var link = Create();
        Assert.Equal(0, link.Id);

        link.AssignGeneratedId(9);

        Assert.Equal(9, link.Id);
        Assert.Throws<InvalidOperationException>(() => link.AssignGeneratedId(10));
    }

    [Fact]
    public void An_identity_needs_a_uuid() =>
        Assert.Throws<ClinicalCareValidationException>(() => EncounterIssueLinkIdentity.Create(Guid.Empty));
}
