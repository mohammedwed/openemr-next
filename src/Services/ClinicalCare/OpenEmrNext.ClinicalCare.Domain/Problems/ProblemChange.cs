using OpenEmrNext.ClinicalCare.Domain.Rules;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Domain.Problems;

/// <summary>
/// The validated facts of an update to a problem (M5c slice 3, ADR 0068): the whole editable set, replacing what is stored. <see cref="Create"/> is the only
/// way to get one, and it applies the same field rules as <see cref="ProblemRecording"/>. Resolving a problem is an update: set <see cref="EndDate"/> (and usually
/// <see cref="Outcome"/> to 1, resolved); reopening it clears the end date.
/// </summary>
/// <remarks>
/// Not editable here, by design: identity (<c>uuid</c>, <c>pid</c>, <c>type</c>), the recorded-at date, who recorded it, and every column a different issue type
/// owns. They are not in this type, so they cannot be in the statement that writes it. Unlike a create, nothing defaults, and the actor is not a field: an update
/// request names none (ADR 0069 #5).
/// </remarks>
public sealed record ProblemChange
{
    public string Title { get; }
    public DateTime BeginDate { get; }
    public DateTime? EndDate { get; }
    public string? Diagnosis { get; }
    public int Outcome { get; }
    public int Occurrence { get; }
    public string? ReferredBy { get; }
    public string? Comments { get; }
    public string? Verification { get; }

    private ProblemChange(
        string title, DateTime beginDate, DateTime? endDate, string? diagnosis, int outcome, int occurrence, string? referredBy, string? comments, string? verification)
    {
        Title = title;
        BeginDate = beginDate;
        EndDate = endDate;
        Diagnosis = diagnosis;
        Outcome = outcome;
        Occurrence = occurrence;
        ReferredBy = referredBy;
        Comments = comments;
        Verification = verification;
    }

    /// <param name="now">The current instant, from the clock (DOM-8).</param>
    /// <param name="codes">The configured code lists, read beforehand by the use case.</param>
    /// <exception cref="ClinicalCareValidationException">One or more fields fail validation.</exception>
    public static ProblemChange Create(
        string? title, DateTime? beginDate, DateTime? endDate, string? diagnosis, int outcome, int occurrence, string? referredBy, string? comments,
        string? verification, DateTime now, IssueCodeData codes)
    {
        var errors = new List<string>();

        var fields = ProblemFieldRules.Validate(title, beginDate, endDate, diagnosis, outcome, occurrence, referredBy, comments, verification, now, codes, errors);

        if (errors.Count > 0)
            throw new ClinicalCareValidationException(errors);

        return new ProblemChange(
            fields.Title!, IssueFieldRules.WholeSeconds(beginDate!.Value), endDate is { } e ? IssueFieldRules.WholeSeconds(e) : null, fields.Diagnosis, outcome,
            occurrence, fields.ReferredBy, fields.Comments, fields.Verification);
    }
}
