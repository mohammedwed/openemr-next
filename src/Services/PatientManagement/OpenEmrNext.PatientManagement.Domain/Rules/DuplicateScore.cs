using OpenEmrNext.PatientManagement.Domain.ValueObjects;

namespace OpenEmrNext.PatientManagement.Domain.Rules;

/// <summary>
/// Classifies a duplicate-detection score the same way legacy OpenEMR's
/// <c>DuplicatePatientService</c> does. The weighted formula that produces the raw score lives in
/// SQL (<c>EfDuplicateScorer</c>, Infrastructure) -- it's a bulk comparison across every other
/// patient row, never two in-memory <see cref="OpenEmrNext.PatientManagement.Domain.Patient"/>
/// objects, so there is no C# formula to express here. What *is* a real domain rule is what the
/// resulting number means.
/// </summary>
public static class DuplicateScore
{
    /// <summary>An operator has declared this chart unique; excluded from scoring forever.</summary>
    public const int Unique = -1;

    /// <summary>Queued for recomputation by a full rescore pass. Not produced by this slice yet.</summary>
    public const int Pending = -9;

    /// <summary>"Two strong identifiers agree" -- worth a human's attention.</summary>
    public const int DisplayThreshold = 12;

    /// <summary>"Three strong identifiers agree" -- a near-certain duplicate.</summary>
    public const int HighlightThreshold = 17;

    public static DuplicateScoreLevel Classify(int score)
    {
        if (score >= HighlightThreshold)
            return DuplicateScoreLevel.NearCertain;

        if (score >= DisplayThreshold)
            return DuplicateScoreLevel.WorthReviewing;

        return DuplicateScoreLevel.None;
    }
}
