using OpenEmrNext.ClinicalCare.Domain.Rules;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Domain.Problems;

/// <summary>
/// The validated facts needed to record a problem on a patient's list. <see cref="Create"/> is the only way to get one, so
/// <see cref="Problem.Record"/> never re-checks what is already guaranteed (one owner per rule, DOM-2), and every violation is collected before
/// failing (DOM-3). Rule ids as in docs/clinical-care/08-record-problem-slice.md.
/// </summary>
public sealed record ProblemRecording
{
    /// <summary>Shared with allergies (<see cref="IssueFieldRules"/>); kept here under the names the problem rules and their tests use.</summary>
    public const int MinTitleLength = IssueFieldRules.MinTitleLength, MaxTitleLength = IssueFieldRules.MaxTitleLength;
    public const int MinDiagnosisLength = IssueFieldRules.MinDiagnosisLength, MaxDiagnosisLength = IssueFieldRules.MaxDiagnosisLength;
    public const int MaxCommentsLength = IssueFieldRules.MaxCommentsLength;
    public const long MaxProviderId = IssueFieldRules.MaxProviderId;

    /// <summary><c>lists.referredby</c> is varchar(255).</summary>
    public const int MaxReferredByLength = 255;

    public static readonly DateTime MinDate = IssueFieldRules.MinDate;
    public static readonly TimeSpan MaxFutureOffset = IssueFieldRules.MaxFutureOffset;

    public PatientReference Patient { get; }
    public string Title { get; }
    public DateTime BeginDate { get; }
    public DateTime? EndDate { get; }
    public string? Diagnosis { get; }
    public int Outcome { get; }
    public int Occurrence { get; }
    public string? ReferredBy { get; }
    public string? Comments { get; }
    public long ProviderId { get; }

    private ProblemRecording(
        PatientReference patient, string title, DateTime beginDate, DateTime? endDate, string? diagnosis, int outcome, int occurrence,
        string? referredBy, string? comments, long providerId)
    {
        Patient = patient;
        Title = title;
        BeginDate = beginDate;
        EndDate = endDate;
        Diagnosis = diagnosis;
        Outcome = outcome;
        Occurrence = occurrence;
        ReferredBy = referredBy;
        Comments = comments;
        ProviderId = providerId;
    }

    /// <param name="patient">The patient (existence is checked by the use case).</param>
    /// <param name="title">The problem's name. Required.</param>
    /// <param name="beginDate">When it began. Required (legacy's validator requires <c>begdate</c>).</param>
    /// <param name="endDate">When it ended, or <c>null</c> while it is active.</param>
    /// <param name="diagnosis">Optional coded diagnosis, <c>SYSTEM:code</c> pairs.</param>
    /// <param name="outcome">An <c>outcome</c> list code; <c>0</c> is "unassigned".</param>
    /// <param name="occurrence">An <c>occurrence</c> list code; <c>0</c> is "unknown".</param>
    /// <param name="providerId">The acting provider; caller-supplied until identity forwarding exists (F-CC-1).</param>
    /// <param name="now">The current instant, from the clock (DOM-8).</param>
    /// <param name="codes">The configured code lists, read beforehand by the use case.</param>
    /// <exception cref="ClinicalCareValidationException">One or more fields fail validation.</exception>
    public static ProblemRecording Create(
        PatientReference patient, string? title, DateTime? beginDate, DateTime? endDate, string? diagnosis, int outcome, int occurrence,
        string? referredBy, string? comments, long providerId, DateTime now, IssueCodeData codes)
    {
        var errors = new List<string>();

        // PB-1 to PB-4, PB-8, PB-9: the rules a problem shares with an allergy (IssueFieldRules).
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

        IssueFieldRules.Comments(comments, errors);
        IssueFieldRules.Provider(providerId, errors);

        if (errors.Count > 0)
            throw new ClinicalCareValidationException(errors);

        return new ProblemRecording(
            patient, trimmedTitle!, IssueFieldRules.WholeSeconds(beginDate!.Value), endDate is { } e ? IssueFieldRules.WholeSeconds(e) : null, trimmedDiagnosis, outcome,
            occurrence, string.IsNullOrWhiteSpace(referredBy) ? null : referredBy.Trim(), string.IsNullOrWhiteSpace(comments) ? null : comments,
            providerId);
    }
}
