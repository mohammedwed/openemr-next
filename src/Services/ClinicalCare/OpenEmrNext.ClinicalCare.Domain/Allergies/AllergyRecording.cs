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
    public const int MaxReactionLength = AllergyFieldRules.MaxReactionLength, MaxSeverityLength = AllergyFieldRules.MaxSeverityLength,
        MaxVerificationLength = AllergyFieldRules.MaxVerificationLength;

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

        // AL-1 to AL-7 and AL-9: the rules an allergy's facts obey, shared with AllergyChange (AllergyFieldRules); AL-8 is about who acts.
        var fields = AllergyFieldRules.Validate(title, beginDate, endDate, diagnosis, reaction, severity, verification, comments, now, codes, errors);
        IssueFieldRules.Provider(providerId, errors);

        if (errors.Count > 0)
            throw new ClinicalCareValidationException(errors);

        return new AllergyRecording(
            patient, fields.Title!, beginDate is { } b ? IssueFieldRules.WholeSeconds(b) : null, endDate is { } e ? IssueFieldRules.WholeSeconds(e) : null,
            fields.Diagnosis, fields.Reaction, fields.Severity, fields.Verification, fields.Comments, providerId);
    }
}
