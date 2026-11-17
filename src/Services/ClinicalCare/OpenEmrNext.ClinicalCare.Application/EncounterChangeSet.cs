using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Application;

/// <summary>The wire names of the editable encounter fields, as audit rows and events list them (names, never values; SEC-9, EVT-5).</summary>
public static class EncounterFieldNames
{
    public const string ClassCode = "classCode";
    public const string CalendarCategoryId = "calendarCategoryId";
    public const string Date = "date";
    public const string OnsetDate = "onsetDate";
    public const string Reason = "reason";
    public const string FacilityId = "facilityId";
    public const string PosCode = "posCode";
    public const string ProviderId = "providerId";
}

/// <summary>
/// Which editable fields an <see cref="EncounterChange"/> would actually change. A pure function of the stored view and the validated
/// change, so "did anything change?" has one answer for the event, the audit row and the decision to write at all.
/// </summary>
public static class EncounterChangeSet
{
    /// <returns>The names of the fields that differ, in a stable order; empty when the change would leave the record as it is.</returns>
    public static IReadOnlyList<string> Between(EncounterView current, EncounterChange change)
    {
        var changed = new List<string>();

        if (!string.Equals(current.ClassCode, change.ClassCode, StringComparison.Ordinal))
            changed.Add(EncounterFieldNames.ClassCode);
        if (current.CalendarCategoryId != change.CalendarCategoryId)
            changed.Add(EncounterFieldNames.CalendarCategoryId);
        if (current.Date != change.Date)
            changed.Add(EncounterFieldNames.Date);
        if (current.OnsetDate != change.OnsetDate)
            changed.Add(EncounterFieldNames.OnsetDate);
        // Legacy stores a blank reason as either NULL or an empty string; the two are the same reason.
        if (!string.Equals(current.Reason ?? "", change.Reason ?? "", StringComparison.Ordinal))
            changed.Add(EncounterFieldNames.Reason);
        if (current.FacilityId != change.FacilityId)
            changed.Add(EncounterFieldNames.FacilityId);
        if (current.PosCode != change.PosCode)
            changed.Add(EncounterFieldNames.PosCode);
        if (current.ProviderId != change.ProviderId)
            changed.Add(EncounterFieldNames.ProviderId);

        return changed;
    }
}
