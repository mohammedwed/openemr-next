using System.Text.RegularExpressions;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Domain.Problems;

/// <summary>
/// The validated facts needed to record a problem on a patient's list. <see cref="Create"/> is the only way to get one, so
/// <see cref="Problem.Record"/> never re-checks what is already guaranteed (one owner per rule, DOM-2), and every violation is collected before
/// failing (DOM-3). Rule ids as in docs/clinical-care/08-record-problem-slice.md.
/// </summary>
public sealed partial record ProblemRecording
{
    /// <summary>Legacy's validator: a title is 2 to 255 characters (<c>ConditionValidator</c>); <c>lists.title</c> is varchar(255).</summary>
    public const int MinTitleLength = 2, MaxTitleLength = 255;

    /// <summary><c>lists.diagnosis</c> is varchar(255); legacy's validator also requires at least 2 characters when present.</summary>
    public const int MinDiagnosisLength = 2, MaxDiagnosisLength = 255;

    /// <summary><c>lists.referredby</c> is varchar(255).</summary>
    public const int MaxReferredByLength = 255;

    /// <summary>(new) Input bound (SEC-7). <c>lists.comments</c> is a longtext with no application limit.</summary>
    public const int MaxCommentsLength = 65_535;

    /// <summary>The provider id is stored in <c>lists.user</c> through a lookup; the bound is the users table's own int id (review finding R-1).</summary>
    public const long MaxProviderId = int.MaxValue;

    /// <summary>(new) The same plausible range as an encounter's dates (CC-17): a year-1 begin date is a typo that sorts to the bottom of every list.</summary>
    public static readonly DateTime MinDate = new(1900, 1, 1);

    /// <summary>(new) How far past <c>now</c> a date may be.</summary>
    public static readonly TimeSpan MaxFutureOffset = TimeSpan.FromDays(366);

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

    /// <summary>Pairs of <c>SYSTEM:code</c> separated by semicolons, as legacy writes them (<c>ICD10:J45.909;SNOMED-CT:195967001</c>).</summary>
    [GeneratedRegex(@"^[A-Za-z0-9._\-]+:[^;\s][^;]*(;[A-Za-z0-9._\-]+:[^;\s][^;]*)*$", RegexOptions.CultureInvariant)]
    private static partial Regex DiagnosisShape();

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

        // PB-1
        var trimmedTitle = title?.Trim();
        if (string.IsNullOrEmpty(trimmedTitle))
            errors.Add("A title is required.");
        else if (trimmedTitle.Length is < MinTitleLength or > MaxTitleLength)
            errors.Add($"Title must be between {MinTitleLength} and {MaxTitleLength} characters.");

        // PB-2
        if (beginDate is null)
            errors.Add("A begin date is required.");
        else if (beginDate < MinDate || beginDate > now + MaxFutureOffset)
            errors.Add($"Begin date must be between {MinDate:yyyy-MM-dd} and one year from now.");

        // PB-3
        if (endDate is { } end)
        {
            if (end < MinDate || end > now + MaxFutureOffset)
                errors.Add($"End date must be between {MinDate:yyyy-MM-dd} and one year from now.");
            else if (beginDate is { } begin && end < begin)
                errors.Add("End date cannot be before the begin date.");
        }

        // PB-4
        var trimmedDiagnosis = string.IsNullOrWhiteSpace(diagnosis) ? null : diagnosis.Trim();
        if (trimmedDiagnosis is not null)
        {
            if (trimmedDiagnosis.Length is < MinDiagnosisLength or > MaxDiagnosisLength)
                errors.Add($"Diagnosis must be between {MinDiagnosisLength} and {MaxDiagnosisLength} characters.");
            else if (!DiagnosisShape().IsMatch(trimmedDiagnosis))
                errors.Add("Diagnosis must be code pairs written SYSTEM:code, separated by semicolons (for example ICD10:J45.909).");
        }

        // PB-5, PB-6: a code must be one the site has configured; 0 ("unassigned" or "unknown") is always allowed, as the column's own default.
        if (outcome != 0 && !codes.Outcomes.Contains(outcome))
            errors.Add($"Outcome {outcome} is not a known outcome.");
        if (occurrence != 0 && !codes.Occurrences.Contains(occurrence))
            errors.Add($"Occurrence {occurrence} is not a known occurrence.");

        // PB-7, PB-8
        if (referredBy is { Length: > MaxReferredByLength })
            errors.Add($"Referred by must be at most {MaxReferredByLength} characters.");
        if (comments is { Length: > MaxCommentsLength })
            errors.Add($"Comments must be at most {MaxCommentsLength} characters.");

        // PB-9: legacy stores the session's user; this service needs an identifiable provider until identity forwarding exists.
        if (providerId is <= 0 or > MaxProviderId)
            errors.Add("ProviderId must identify the acting provider.");

        if (errors.Count > 0)
            throw new ClinicalCareValidationException(errors);

        return new ProblemRecording(
            patient, trimmedTitle!, WholeSeconds(beginDate!.Value), endDate is { } e ? WholeSeconds(e) : null, trimmedDiagnosis, outcome,
            occurrence, string.IsNullOrWhiteSpace(referredBy) ? null : referredBy.Trim(), string.IsNullOrWhiteSpace(comments) ? null : comments,
            providerId);
    }

    /// <summary>The columns hold whole seconds, so what is stored and later compared is cut to whole seconds here (as <c>EncounterChange</c> does).</summary>
    private static DateTime WholeSeconds(DateTime value) => new(value.Ticks - value.Ticks % TimeSpan.TicksPerSecond, value.Kind);
}
