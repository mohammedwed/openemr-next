using OpenEmrNext.ClinicalCare.Domain.Problems;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>Builds problem domain objects the same way production does: through the validating factories.</summary>
internal static class ProblemTestData
{
    public static readonly DateTime Now = new(2026, 11, 17, 9, 30, 0);

    public static readonly IssueCodeData Codes = new(
        new HashSet<int> { 0, 1, 2, 3, 4, 5 }, new HashSet<int> { 0, 1, 4, 5, 6, 7, 8 });

    public static Problem NewProblem(
        Guid? uuid = null, long providerId = 7, string title = "Asthma", DateTime? begin = null, DateTime? end = null,
        string? diagnosis = "ICD10:J45.909", int outcome = 0, int occurrence = 1, string? referredBy = null, string? comments = "seasonal",
        Guid? patientUuid = null, long pid = EncounterTestData.PatientPid)
    {
        var recording = ProblemRecording.Create(
            PatientReference.Create(patientUuid ?? EncounterTestData.PatientUuid, pid), title, begin ?? new DateTime(2020, 5, 1), end,
            diagnosis, outcome, occurrence, referredBy, comments, providerId, Now, Codes);
        return Problem.Record(recording, ProblemIdentity.Create(uuid ?? Guid.CreateVersion7()), Now);
    }
}
