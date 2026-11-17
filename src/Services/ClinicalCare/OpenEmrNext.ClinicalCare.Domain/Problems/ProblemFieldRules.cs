using OpenEmrNext.ClinicalCare.Domain.Rules;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Domain.Problems;

/// <summary>
/// The field rules a problem's editable facts obey (PB-1 to PB-8 and PB-21), written once and used by both <see cref="ProblemRecording"/> and
/// <see cref="ProblemChange"/> so a record and an update can never disagree about what is valid (one owner per rule, DOM-2). They in turn use the rules
/// shared with allergies (<see cref="IssueFieldRules"/>).
/// </summary>
internal static class ProblemFieldRules
{
    /// <summary><c>lists.verification</c> is varchar(36).</summary>
    public const int MaxVerificationLength = 36;

    /// <summary><c>lists.referredby</c> is varchar(255).</summary>
    public const int MaxReferredByLength = 255;

    /// <summary>What the rules resolved: the cleaned values to store.</summary>
    internal readonly record struct Resolved(string? Title, string? Diagnosis, string? ReferredBy, string? Comments, string? Verification);

    /// <summary>Checks every field and collects every violation into <paramref name="errors"/> (DOM-3); never throws. The message order is the check order.</summary>
    public static Resolved Validate(
        string? title, DateTime? beginDate, DateTime? endDate, string? diagnosis, int outcome, int occurrence, string? referredBy, string? comments,
        string? verification, DateTime now, IssueCodeData codes, List<string> errors)
    {
        var trimmedTitle = IssueFieldRules.Title(title, errors);
        IssueFieldRules.Dates(beginDate, endDate, beginRequired: true, now, errors);
        var trimmedDiagnosis = IssueFieldRules.Diagnosis(diagnosis, errors);

        // PB-5, PB-6: a code must be one the site has configured; 0 ("unassigned" or "unknown") is always allowed, as the column's own default.
        if (outcome != 0 && !codes.Outcomes.Contains(outcome))
            errors.Add($"Outcome {outcome} is not a known outcome.");
        if (occurrence != 0 && !codes.Occurrences.Contains(occurrence))
            errors.Add($"Occurrence {occurrence} is not a known occurrence.");

        // PB-7
        if (referredBy is { Length: > MaxReferredByLength })
            errors.Add($"Referred by must be at most {MaxReferredByLength} characters.");

        // PB-21: a problem's verification is a code of the condition-verification list (an allergy's is a different list).
        var resolvedVerification = IssueFieldRules.StringCode(verification, "Verification", codes.Verifications, MaxVerificationLength, errors);

        IssueFieldRules.Comments(comments, errors);

        return new Resolved(
            trimmedTitle, trimmedDiagnosis, string.IsNullOrWhiteSpace(referredBy) ? null : referredBy.Trim(),
            string.IsNullOrWhiteSpace(comments) ? null : comments, resolvedVerification);
    }
}
