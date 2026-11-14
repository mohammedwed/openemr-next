using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Domain;
using OpenEmrNext.PatientManagement.Domain.Rules;
using OpenEmrNext.PatientManagement.Domain.ValueObjects;

namespace OpenEmrNext.PatientManagement.Application;

/// <summary>
/// Recomputes and caches a patient's duplicate-detection score, matching legacy OpenEMR's own
/// behavior of rescoring synchronously right after a new patient is inserted
/// (<c>library/patient.inc.php</c>'s call into <c>DuplicatePatientService::recalculateScore()</c>).
/// Deliberately a separate use case from <c>RegisterPatientUseCase</c>, not folded into it --
/// registration's own job is registering a patient, not scoring it.
/// </summary>
public sealed class ScorePatientForDuplicatesUseCase(IDuplicateScorer scorer)
{
    public async Task<DuplicateScoreLevel> ExecuteAsync(long pid, CancellationToken ct)
    {
        var score = await scorer.RescoreAsync(pid, ct);
        return DuplicateScore.Classify(score);
    }
}
