using System.Text.RegularExpressions;

namespace OpenEmrNext.ClinicalCare.Domain.Rules;

/// <summary>
/// The field rules every issue type this service owns shares (problems and allergies, ADR 0070), written once so the two aggregates can never
/// disagree about what a valid title, date range or diagnosis is (one owner per rule, DOM-2). Messages are the wire text callers see.
/// </summary>
internal static partial class IssueFieldRules
{
    /// <summary>Legacy's validators: a title is 2 to 255 characters (<c>ConditionValidator</c>, <c>AllergyIntoleranceValidator</c>); <c>lists.title</c> is varchar(255).</summary>
    public const int MinTitleLength = 2, MaxTitleLength = 255;

    /// <summary><c>lists.diagnosis</c> is varchar(255); legacy's validators also require at least 2 characters when present.</summary>
    public const int MinDiagnosisLength = 2, MaxDiagnosisLength = 255;

    /// <summary>(new) Input bound (SEC-7). <c>lists.comments</c> is a longtext with no application limit.</summary>
    public const int MaxCommentsLength = 65_535;

    /// <summary>The provider id is stored through a lookup of the users table's own int id (review finding R-1).</summary>
    public const long MaxProviderId = int.MaxValue;

    /// <summary>(new) The same plausible range as an encounter's dates (CC-17): a year-1 date is a typo that sorts to the bottom of every list.</summary>
    public static readonly DateTime MinDate = new(1900, 1, 1);

    /// <summary>(new) How far past <c>now</c> a date may be.</summary>
    public static readonly TimeSpan MaxFutureOffset = TimeSpan.FromDays(366);

    /// <summary>Pairs of <c>SYSTEM:code</c> separated by semicolons, as legacy writes them (<c>ICD10:J45.909;SNOMED-CT:195967001</c>).</summary>
    [GeneratedRegex(@"^[A-Za-z0-9._\-]+:[^;\s][^;]*(;[A-Za-z0-9._\-]+:[^;\s][^;]*)*$", RegexOptions.CultureInvariant)]
    private static partial Regex DiagnosisShape();

    /// <returns>The trimmed title, or <c>null</c> (with a message added) when it is missing or the wrong length.</returns>
    public static string? Title(string? title, List<string> errors)
    {
        var trimmed = title?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            errors.Add("A title is required.");
            return null;
        }

        if (trimmed.Length is < MinTitleLength or > MaxTitleLength)
        {
            errors.Add($"Title must be between {MinTitleLength} and {MaxTitleLength} characters.");
            return null;
        }

        return trimmed;
    }

    /// <summary>Checks a begin date (required or optional) and an end date against the range and each other. Returns nothing: the caller keeps what it was given.</summary>
    public static void Dates(DateTime? beginDate, DateTime? endDate, bool beginRequired, DateTime now, List<string> errors)
    {
        if (beginDate is null)
        {
            if (beginRequired)
                errors.Add("A begin date is required.");
        }
        else if (beginDate < MinDate || beginDate > now + MaxFutureOffset)
        {
            errors.Add($"Begin date must be between {MinDate:yyyy-MM-dd} and one year from now.");
        }

        if (endDate is { } end)
        {
            if (end < MinDate || end > now + MaxFutureOffset)
                errors.Add($"End date must be between {MinDate:yyyy-MM-dd} and one year from now.");
            else if (beginDate is { } begin && end < begin)
                errors.Add("End date cannot be before the begin date.");
        }
    }

    /// <returns>The trimmed diagnosis, or <c>null</c> when none was given (blank is none) or it is invalid (with a message added).</returns>
    public static string? Diagnosis(string? diagnosis, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(diagnosis))
            return null;

        var trimmed = diagnosis.Trim();
        if (trimmed.Length is < MinDiagnosisLength or > MaxDiagnosisLength)
        {
            errors.Add($"Diagnosis must be between {MinDiagnosisLength} and {MaxDiagnosisLength} characters.");
            return null;
        }

        if (!DiagnosisShape().IsMatch(trimmed))
        {
            errors.Add("Diagnosis must be code pairs written SYSTEM:code, separated by semicolons (for example ICD10:J45.909).");
            return null;
        }

        return trimmed;
    }

    /// <summary>
    /// A string code (<c>reaction</c>, <c>severity_al</c>, <c>verification</c>): compared exactly with the configured option ids (as <c>option_id</c> values are), and
    /// no longer than the column that stores it. The width check guards the column, not the list: a site can configure an option id longer than its column, and that
    /// must be a clean message, not a database error (review findings R-1 to R-7). Blank means none.
    /// </summary>
    public static string? StringCode(string? value, string label, IReadOnlySet<string> allowed, int maxLength, List<string> errors)
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

    public static void Comments(string? comments, List<string> errors)
    {
        if (comments is { Length: > MaxCommentsLength })
            errors.Add($"Comments must be at most {MaxCommentsLength} characters.");
    }

    public static void Provider(long providerId, List<string> errors)
    {
        if (providerId is <= 0 or > MaxProviderId)
            errors.Add("ProviderId must identify the acting provider.");
    }

    /// <summary>The columns hold whole seconds, so what is stored and later compared is cut to whole seconds.</summary>
    public static DateTime WholeSeconds(DateTime value) => new(value.Ticks - value.Ticks % TimeSpan.TicksPerSecond, value.Kind);
}
