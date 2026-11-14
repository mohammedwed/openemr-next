namespace OpenEmrNext.PatientManagement.Domain.ValueObjects;

/// <summary>How seriously a duplicate-detection score should be treated, matching legacy OpenEMR's
/// own two-tier report (DuplicatePatientService.php): "two strong identifiers agree" vs. "three".</summary>
public enum DuplicateScoreLevel
{
    None,
    WorthReviewing,
    NearCertain,
}
