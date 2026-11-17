using OpenEmrNext.ClinicalCare.Domain.Rules;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Domain.Allergies;

/// <summary>
/// The validated facts of an update to an allergy (M5c slice 3, ADR 0068): the whole editable set, replacing what is stored. <see cref="Create"/> is the only way
/// to get one, and it applies the same field rules as <see cref="AllergyRecording"/>. Ending an allergy is an update: set <see cref="EndDate"/>; reopening clears it.
/// </summary>
/// <remarks>
/// Not editable here, by design: identity, the recorded-at date, who recorded it, and every column a different issue type owns. Unlike a create, nothing defaults,
/// and the actor is not a field (ADR 0069 #5).
/// </remarks>
public sealed record AllergyChange
{
    public string Title { get; }
    public DateTime? BeginDate { get; }
    public DateTime? EndDate { get; }
    public string? Diagnosis { get; }
    public string? Reaction { get; }
    public string? Severity { get; }
    public string? Verification { get; }
    public string? Comments { get; }

    private AllergyChange(
        string title, DateTime? beginDate, DateTime? endDate, string? diagnosis, string? reaction, string? severity, string? verification, string? comments)
    {
        Title = title;
        BeginDate = beginDate;
        EndDate = endDate;
        Diagnosis = diagnosis;
        Reaction = reaction;
        Severity = severity;
        Verification = verification;
        Comments = comments;
    }

    /// <param name="now">The current instant, from the clock (DOM-8).</param>
    /// <param name="codes">The configured code lists, read beforehand by the use case.</param>
    /// <exception cref="ClinicalCareValidationException">One or more fields fail validation.</exception>
    public static AllergyChange Create(
        string? title, DateTime? beginDate, DateTime? endDate, string? diagnosis, string? reaction, string? severity, string? verification, string? comments,
        DateTime now, AllergyCodeData codes)
    {
        var errors = new List<string>();

        var fields = AllergyFieldRules.Validate(title, beginDate, endDate, diagnosis, reaction, severity, verification, comments, now, codes, errors);

        if (errors.Count > 0)
            throw new ClinicalCareValidationException(errors);

        return new AllergyChange(
            fields.Title!, beginDate is { } b ? IssueFieldRules.WholeSeconds(b) : null, endDate is { } e ? IssueFieldRules.WholeSeconds(e) : null,
            fields.Diagnosis, fields.Reaction, fields.Severity, fields.Verification, fields.Comments);
    }
}
