using OpenEmrNext.ClinicalCare.Domain.Rules;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Domain.Allergies;

/// <summary>
/// The validated facts needed to record an allergy on a patient's list. <see cref="Create"/> is the only way to get one, so <see cref="Allergy.Record"/>
/// never re-checks what is already guaranteed (one owner per rule, DOM-2), and every violation is collected before failing (DOM-3). Rule ids AL-1 to AL-9
/// as in docs/clinical-care/09-record-allergy-slice.md; title, dates and diagnosis share their rules with problems (<see cref="IssueFieldRules"/>).
/// </summary>
public sealed record AllergyRecording
{
    /// <summary>The column widths of the three coded fields: <c>reaction</c> varchar(255), <c>severity_al</c> varchar(50), <c>verification</c> varchar(36).</summary>
    public const int MaxReactionLength = 255, MaxSeverityLength = 50, MaxVerificationLength = 36;

    public PatientReference Patient { get; }
    public string Title { get; }
    public DateTime? BeginDate { get; }
    public DateTime? EndDate { get; }
    public string? Diagnosis { get; }
    public string? Reaction { get; }
    public string? Severity { get; }
    public string? Verification { get; }
    public string? Comments { get; }
    public long ProviderId { get; }

    private AllergyRecording(
        PatientReference patient, string title, DateTime? beginDate, DateTime? endDate, string? diagnosis, string? reaction, string? severity,
        string? verification, string? comments, long providerId)
    {
        Patient = patient;
        Title = title;
        BeginDate = beginDate;
        EndDate = endDate;
        Diagnosis = diagnosis;
        Reaction = reaction;
        Severity = severity;
        Verification = verification;
        Comments = comments;
        ProviderId = providerId;
    }

    /// <param name="patient">The patient (existence is checked by the use case).</param>
    /// <param name="title">The substance. Required, 2 to 255 characters.</param>
    /// <param name="beginDate">When the allergy began, or <c>null</c>: unlike a problem, legacy does not require it.</param>
    /// <param name="endDate">When it ended, or <c>null</c> while it is active.</param>
    /// <param name="diagnosis">Optional coded diagnosis, <c>SYSTEM:code</c> pairs.</param>
    /// <param name="reaction">A <c>reaction</c> list code, or blank for none.</param>
    /// <param name="severity">A <c>severity_ccda</c> list code, or blank for none.</param>
    /// <param name="verification">An <c>allergyintolerance-verification</c> list code, or blank for none.</param>
    /// <param name="providerId">The acting provider; caller-supplied until identity forwarding exists (F-CC-1).</param>
    /// <param name="now">The current instant, from the clock (DOM-8).</param>
    /// <param name="codes">The configured code lists, read beforehand by the use case.</param>
    /// <exception cref="ClinicalCareValidationException">One or more fields fail validation.</exception>
    public static AllergyRecording Create(
        PatientReference patient, string? title, DateTime? beginDate, DateTime? endDate, string? diagnosis, string? reaction, string? severity,
        string? verification, string? comments, long providerId, DateTime now, AllergyCodeData codes)
    {
        var errors = new List<string>();

        // AL-1 to AL-3, AL-8, AL-9
        var trimmedTitle = IssueFieldRules.Title(title, errors);
        IssueFieldRules.Dates(beginDate, endDate, beginRequired: false, now, errors);
        var trimmedDiagnosis = IssueFieldRules.Diagnosis(diagnosis, errors);

        // AL-4 to AL-6: a code must be one the site has configured and one its column can hold; blank means none.
        var resolvedReaction = Code(reaction, "Reaction", codes.Reactions, MaxReactionLength, errors);
        var resolvedSeverity = Code(severity, "Severity", codes.Severities, MaxSeverityLength, errors);
        var resolvedVerification = Code(verification, "Verification", codes.Verifications, MaxVerificationLength, errors);

        IssueFieldRules.Comments(comments, errors);
        IssueFieldRules.Provider(providerId, errors);

        if (errors.Count > 0)
            throw new ClinicalCareValidationException(errors);

        return new AllergyRecording(
            patient, trimmedTitle!, beginDate is { } b ? IssueFieldRules.WholeSeconds(b) : null, endDate is { } e ? IssueFieldRules.WholeSeconds(e) : null,
            trimmedDiagnosis, resolvedReaction, resolvedSeverity, resolvedVerification, string.IsNullOrWhiteSpace(comments) ? null : comments, providerId);
    }

    /// <summary>
    /// A code is compared exactly with the configured option ids (as <c>option_id</c> values are). The width check guards the column, not the list: a site can
    /// configure an option id longer than the column that stores it, and that must be a clean message, not a database error (review findings R-1 to R-7).
    /// </summary>
    private static string? Code(string? value, string label, IReadOnlySet<string> allowed, int maxLength, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (!allowed.Contains(value))
        {
            errors.Add($"{label} '{value}' is not a known {label.ToLowerInvariant()}.");
            return null;
        }

        if (value.Length > maxLength)
        {
            errors.Add($"{label} '{value}' is longer than the {maxLength} characters a record can hold.");
            return null;
        }

        return value;
    }
}
