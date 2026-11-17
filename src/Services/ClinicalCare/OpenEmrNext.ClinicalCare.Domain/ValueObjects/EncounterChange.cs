using OpenEmrNext.ClinicalCare.Domain.Rules;

namespace OpenEmrNext.ClinicalCare.Domain.ValueObjects;

/// <summary>
/// The validated facts of an update to an encounter (M5b phase 2, ADR 0068): the whole editable set, replacing what is stored.
/// <see cref="Create"/> is the only way to get one, and it applies the same field rules as <see cref="EncounterCreation"/>.
/// </summary>
/// <remarks>
/// Not editable here, by design: identity (<c>uuid</c>, encounter number, patient), <c>sensitivity</c> (changing it needs the sensitivities
/// ACL, F-CC-2), and every column no slice owns (billing and collection columns, referral and discharge fields). They are not in this type, so
/// they cannot be in the statement that writes it.
/// <para>
/// Unlike a create, nothing here defaults: the date and the class must be given. A <c>PUT</c> replaces the set, and silently turning a
/// missing date into "now" or a missing class into the list default would rewrite a field the caller never mentioned.
/// </para>
/// </remarks>
public sealed record EncounterChange
{
    public string ClassCode { get; }
    public int CalendarCategoryId { get; }
    public DateTime Date { get; }
    public DateTime? OnsetDate { get; }
    public string? Reason { get; }
    public int FacilityId { get; }
    public string? FacilityName { get; }
    public int? PosCode { get; }
    public long ProviderId { get; }

    private EncounterChange(
        string classCode, int calendarCategoryId, DateTime date, DateTime? onsetDate, string? reason,
        int facilityId, string? facilityName, int? posCode, long providerId)
    {
        ClassCode = classCode;
        CalendarCategoryId = calendarCategoryId;
        Date = date;
        OnsetDate = onsetDate;
        Reason = reason;
        FacilityId = facilityId;
        FacilityName = facilityName;
        PosCode = posCode;
        ProviderId = providerId;
    }

    /// <param name="classCode">The class to store; required, and must be in the configured list.</param>
    /// <param name="date">The encounter date; required.</param>
    /// <param name="posCode">The place of service, or <c>null</c> to take the facility's (CC-8).</param>
    /// <param name="providerId">The encounter's provider (a field being set, not who is editing: that is the actor, ADR 0069).</param>
    /// <param name="now">The current instant, from the clock (DOM-8).</param>
    /// <param name="reference">Class list and facility read beforehand by the use case.</param>
    /// <exception cref="ClinicalCareValidationException">One or more fields fail validation.</exception>
    public static EncounterChange Create(
        string? classCode, int calendarCategoryId, DateTime? date, DateTime? onsetDate, string? reason,
        int facilityId, int? posCode, long providerId, DateTime now, EncounterReferenceData reference)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(classCode))
            errors.Add("A class is required.");

        if (date is null)
            errors.Add("A date is required.");

        // A blank class or a missing date is already an error above, so whatever the shared rules resolve for it (the list default, for a
        // blank class) is never used: the update throws before it could be. An update never fills a field from a default.
        var resolved = EncounterFieldRules.Validate(
            classCode, calendarCategoryId, reason, sensitivity: null, providerId,
            "ProviderId must identify the encounter's provider.", date, onsetDate, facilityId, posCode, now, reference, errors);

        if (errors.Count > 0)
            throw new ClinicalCareValidationException(errors);

        return new EncounterChange(
            resolved.ClassCode!, calendarCategoryId, WholeSeconds(date!.Value), onsetDate is { } onset ? WholeSeconds(onset) : null, reason,
            facilityId, resolved.FacilityName, resolved.PosCode, providerId);
    }

    /// <summary>
    /// The columns hold whole seconds, so what is compared with them and written to them is cut to whole seconds here: otherwise a
    /// resubmitted, unchanged date with a stray fraction would look like a change.
    /// </summary>
    private static DateTime WholeSeconds(DateTime value) => new(value.Ticks - value.Ticks % TimeSpan.TicksPerSecond, value.Kind);
}
