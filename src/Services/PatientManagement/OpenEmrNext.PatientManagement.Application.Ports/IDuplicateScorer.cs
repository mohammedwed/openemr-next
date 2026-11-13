namespace OpenEmrNext.PatientManagement.Application.Ports;

/// <summary>
/// What <c>ScorePatientForDuplicatesUseCase</c> needs: recompute one patient's duplicate score
/// against every other patient and persist it, returning the raw score. How that comparison is
/// actually done (a bulk SQL scan, matching legacy OpenEMR's own approach) is entirely the
/// adapter's concern -- Application never sees it.
/// </summary>
public interface IDuplicateScorer
{
    Task<int> RescoreAsync(long pid, CancellationToken ct);
}
