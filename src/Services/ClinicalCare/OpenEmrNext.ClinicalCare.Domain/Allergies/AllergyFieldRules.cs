using OpenEmrNext.ClinicalCare.Domain.Rules;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Domain.Allergies;

/// <summary>
/// The field rules an allergy's editable facts obey (AL-1 to AL-7 and AL-9), written once and used by both <see cref="AllergyRecording"/> and
/// <see cref="AllergyChange"/> so a record and an update can never disagree about what is valid (one owner per rule, DOM-2).
/// </summary>
internal static class AllergyFieldRules
{
    /// <summary>The column widths of the three coded fields: <c>reaction</c> varchar(255), <c>severity_al</c> varchar(50), <c>verification</c> varchar(36).</summary>
    public const int MaxReactionLength = 255, MaxSeverityLength = 50, MaxVerificationLength = 36;

    internal readonly record struct Resolved(
        string? Title, string? Diagnosis, string? Reaction, string? Severity, string? Verification, string? Comments);

    /// <summary>Checks every field and collects every violation into <paramref name="errors"/> (DOM-3); never throws. The message order is the check order.</summary>
    public static Resolved Validate(
        string? title, DateTime? beginDate, DateTime? endDate, string? diagnosis, string? reaction, string? severity, string? verification, string? comments,
        DateTime now, AllergyCodeData codes, List<string> errors)
    {
        var trimmedTitle = IssueFieldRules.Title(title, errors);
        IssueFieldRules.Dates(beginDate, endDate, beginRequired: false, now, errors);
        var trimmedDiagnosis = IssueFieldRules.Diagnosis(diagnosis, errors);

        // AL-4 to AL-6: a code must be one the site has configured and one its column can hold; blank means none.
        var resolvedReaction = IssueFieldRules.StringCode(reaction, "Reaction", codes.Reactions, MaxReactionLength, errors);
        var resolvedSeverity = IssueFieldRules.StringCode(severity, "Severity", codes.Severities, MaxSeverityLength, errors);
        var resolvedVerification = IssueFieldRules.StringCode(verification, "Verification", codes.Verifications, MaxVerificationLength, errors);

        IssueFieldRules.Comments(comments, errors);

        return new Resolved(
            trimmedTitle, trimmedDiagnosis, resolvedReaction, resolvedSeverity, resolvedVerification, string.IsNullOrWhiteSpace(comments) ? null : comments);
    }
}
