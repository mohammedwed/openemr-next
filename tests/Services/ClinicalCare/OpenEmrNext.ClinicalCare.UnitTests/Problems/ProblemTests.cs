using OpenEmrNext.ClinicalCare.Domain.Problems;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.UnitTests.Problems;

/// <summary>The aggregate: identity handed in, the recorded facts kept, the database key assigned once (DOM-5, DOM-6, DOM-7).</summary>
public class ProblemTests
{
    private static readonly DateTime Now = new(2026, 11, 17, 9, 30, 0);
    private static readonly IssueCodeData Codes = new(new HashSet<int> { 0, 1 }, new HashSet<int> { 0, 1 });

    private static Problem NewProblem(Guid? uuid = null)
    {
        var recording = ProblemRecording.Create(
            PatientReference.Create(Guid.NewGuid(), 42), "Asthma", new DateTime(2020, 5, 1), null, "ICD10:J45.909", 0, 1, null, "note", 7, Now, Codes);
        return Problem.Record(recording, ProblemIdentity.Create(uuid ?? Guid.NewGuid()), Now);
    }

    [Fact]
    public void A_recorded_problem_carries_the_identity_it_was_given_and_the_facts_recorded()
    {
        var uuid = Guid.NewGuid();

        var problem = NewProblem(uuid);

        Assert.Equal(uuid, problem.Uuid);
        Assert.Equal("Asthma", problem.Title);
        Assert.Equal(new DateTime(2020, 5, 1), problem.BeginDate);
        Assert.Null(problem.EndDate);
        Assert.Equal("ICD10:J45.909", problem.Diagnosis);
        Assert.Equal(1, problem.Occurrence);
        Assert.Equal("note", problem.Comments);
        Assert.Equal(7, problem.ProviderId);
        Assert.Equal(42, problem.Patient.Pid);
    }

    [Fact]
    public void The_recorded_time_is_the_clocks_now_not_the_date_the_problem_began() =>
        Assert.Equal(Now, NewProblem().RecordedAt);

    [Fact]
    public void The_database_id_is_zero_until_assigned_and_can_be_assigned_once()
    {
        var problem = NewProblem();
        Assert.Equal(0, problem.Id);

        problem.AssignGeneratedId(55);

        Assert.Equal(55, problem.Id);
        Assert.Throws<InvalidOperationException>(() => problem.AssignGeneratedId(56));
    }

    [Fact]
    public void An_identity_needs_a_uuid() =>
        Assert.Throws<ClinicalCareValidationException>(() => ProblemIdentity.Create(Guid.Empty));
}
