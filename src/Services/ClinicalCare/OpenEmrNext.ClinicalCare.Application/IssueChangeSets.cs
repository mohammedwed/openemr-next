using OpenEmrNext.ClinicalCare.Domain.Allergies;
using OpenEmrNext.ClinicalCare.Domain.Problems;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;

namespace OpenEmrNext.ClinicalCare.Application;

/// <summary>The wire names of the editable problem and allergy fields, as audit rows and events list them (names, never values; SEC-9, EVT-5).</summary>
public static class IssueFieldNames
{
    public const string Title = "title";
    public const string BeginDate = "beginDate";
    public const string EndDate = "endDate";
    public const string Diagnosis = "diagnosis";
    public const string Outcome = "outcome";
    public const string Occurrence = "occurrence";
    public const string ReferredBy = "referredBy";
    public const string Comments = "comments";
    public const string Verification = "verification";
    public const string Reaction = "reaction";
    public const string Severity = "severity";
}

/// <summary>
/// Which editable fields a <see cref="ProblemChange"/> would actually change. A pure function of the stored view and the validated change, so "did anything
/// change?" has one answer for the event, the audit row and the decision to write at all.
/// </summary>
public static class ProblemChangeSet
{
    /// <returns>The names of the fields that differ, in a stable order; empty when the change would leave the record as it is.</returns>
    public static IReadOnlyList<string> Between(ProblemView current, ProblemChange change)
    {
        var changed = new List<string>();

        if (!SameText(current.Title, change.Title)) changed.Add(IssueFieldNames.Title);
        if (current.BeginDate != change.BeginDate) changed.Add(IssueFieldNames.BeginDate);
        if (current.EndDate != change.EndDate) changed.Add(IssueFieldNames.EndDate);
        if (!SameText(current.Diagnosis, change.Diagnosis)) changed.Add(IssueFieldNames.Diagnosis);
        if (current.Outcome != change.Outcome) changed.Add(IssueFieldNames.Outcome);
        if (current.Occurrence != change.Occurrence) changed.Add(IssueFieldNames.Occurrence);
        if (!SameText(current.ReferredBy, change.ReferredBy)) changed.Add(IssueFieldNames.ReferredBy);
        if (!SameText(current.Comments, change.Comments)) changed.Add(IssueFieldNames.Comments);
        if (!SameText(current.Verification, change.Verification)) changed.Add(IssueFieldNames.Verification);

        return changed;
    }

    /// <summary>Legacy stores a blank text as either NULL or an empty string; the two are the same value.</summary>
    internal static bool SameText(string? a, string? b) => string.Equals(a ?? "", b ?? "", StringComparison.Ordinal);
}

/// <summary>Which editable fields an <see cref="AllergyChange"/> would actually change (see <see cref="ProblemChangeSet"/>).</summary>
public static class AllergyChangeSet
{
    /// <returns>The names of the fields that differ, in a stable order; empty when the change would leave the record as it is.</returns>
    public static IReadOnlyList<string> Between(AllergyView current, AllergyChange change)
    {
        var changed = new List<string>();

        if (!ProblemChangeSet.SameText(current.Title, change.Title)) changed.Add(IssueFieldNames.Title);
        if (current.BeginDate != change.BeginDate) changed.Add(IssueFieldNames.BeginDate);
        if (current.EndDate != change.EndDate) changed.Add(IssueFieldNames.EndDate);
        if (!ProblemChangeSet.SameText(current.Diagnosis, change.Diagnosis)) changed.Add(IssueFieldNames.Diagnosis);
        if (!ProblemChangeSet.SameText(current.Reaction, change.Reaction)) changed.Add(IssueFieldNames.Reaction);
        if (!ProblemChangeSet.SameText(current.Severity, change.Severity)) changed.Add(IssueFieldNames.Severity);
        if (!ProblemChangeSet.SameText(current.Verification, change.Verification)) changed.Add(IssueFieldNames.Verification);
        if (!ProblemChangeSet.SameText(current.Comments, change.Comments)) changed.Add(IssueFieldNames.Comments);

        return changed;
    }
}
