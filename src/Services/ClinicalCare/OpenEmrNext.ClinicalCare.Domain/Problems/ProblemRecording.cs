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

    public const int MaxVerificationLength = ProblemFieldRules.MaxVerificationLength;
    public const int MaxReferredByLength = ProblemFieldRules.MaxReferredByLength;

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
    public string? Verification { get; }
    public long ProviderId { get; }

    private ProblemRecording(
        PatientReference patient, string title, DateTime beginDate, DateTime? endDate, string? diagnosis, int outcome, int occurrence,
        string? referredBy, string? comments, string? verification, long providerId)
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
        Verification = verification;
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
    /// <param name="verification">A <c>condition-verification</c> code, or blank for none (PB-21).</param>
    /// <exception cref="ClinicalCareValidationException">One or more fields fail validation.</exception>
    public static ProblemRecording Create(
        PatientReference patient, string? title, DateTime? beginDate, DateTime? endDate, string? diagnosis, int outcome, int occurrence,
        string? referredBy, string? comments, long providerId, DateTime now, IssueCodeData codes, string? verification = null)
    {
        var errors = new List<string>();

        // PB-1 to PB-8 and PB-21: the rules a problem's facts obey, shared with ProblemChange (ProblemFieldRules); PB-9 is about who acts.
        var fields = ProblemFieldRules.Validate(title, beginDate, endDate, diagnosis, outcome, occurrence, referredBy, comments, verification, now, codes, errors);
        IssueFieldRules.Provider(providerId, errors);

        if (errors.Count > 0)
            throw new ClinicalCareValidationException(errors);

        return new ProblemRecording(
            patient, fields.Title!, IssueFieldRules.WholeSeconds(beginDate!.Value), endDate is { } e ? IssueFieldRules.WholeSeconds(e) : null, fields.Diagnosis, outcome,
            occurrence, fields.ReferredBy, fields.Comments, fields.Verification, providerId);
    }
}
