using OpenEmrNext.ClinicalCare.Domain.Rules;

namespace OpenEmrNext.ClinicalCare.Domain.ValueObjects;

/// <summary>
/// The validated facts needed to create an encounter. <see cref="Create"/> is the only way to get one, so
/// <see cref="Encounter.Create"/> never re-checks what is already guaranteed (one owner per rule, DOM-2),
/// and every violation is collected before failing (DOM-3).
/// </summary>
public sealed record EncounterCreation
{
    /// <summary>Legacy <c>form_encounter.sensitivity</c> is varchar(30).</summary>
    public const int MaxSensitivityLength = 30;

    /// <summary>
    /// (new) Input bound (SEC-7). Legacy <c>reason</c> is a longtext with no application limit; this stops an
    /// unbounded body without rejecting any realistic note. Marked CC-12 in the rule catalogue.
    /// </summary>
    public const int MaxReasonLength = 65_535;

    /// <summary>
    /// (new) Place-of-service codes are two digits (CMS). <c>form_encounter.pos_code</c> is a signed tinyint,
    /// so anything above 127 would be a raw database error instead of a clean 400 (CC-13).
    /// </summary>
    public const int MaxPosCode = 99;

    /// <summary>
    /// (new) The earliest encounter or onset date accepted (CC-17). Legacy's <c>datetime</c> columns accept almost anything and a
    /// year-1 date is stored without complaint, so a typo becomes a clinical record sorted to the bottom of every list.
    /// </summary>
    public static readonly DateTime MinDate = new(1900, 1, 1);

    /// <summary>(new) How far past <c>now</c> a date may be (CC-17): a scheduled visit is plausible, year 2999 is a typo.</summary>
    public static readonly TimeSpan MaxFutureOffset = TimeSpan.FromDays(366);

    /// <summary>
    /// <c>form_encounter.provider_id</c> is an <c>int(11)</c>: a larger id is a valid <c>long</c> and a value the column cannot
    /// hold, which was a 500 (review finding R-1). The same bound applies to every provider id in this service.
    /// </summary>
    public const long MaxProviderId = int.MaxValue;

    public PatientReference Patient { get; }
    public string ClassCode { get; }
    public int CalendarCategoryId { get; }
    public DateTime Date { get; }
    public DateTime? OnsetDate { get; }
    public string? Reason { get; }
    public int FacilityId { get; }
    public string? FacilityName { get; }
    public int? PosCode { get; }
    public long ProviderId { get; }
    public string? Sensitivity { get; }

    private EncounterCreation(
        PatientReference patient, string classCode, int calendarCategoryId, DateTime date, DateTime? onsetDate,
        string? reason, int facilityId, string? facilityName, int? posCode, long providerId, string? sensitivity)
    {
        Patient = patient;
        ClassCode = classCode;
        CalendarCategoryId = calendarCategoryId;
        Date = date;
        OnsetDate = onsetDate;
        Reason = reason;
        FacilityId = facilityId;
        FacilityName = facilityName;
        PosCode = posCode;
        ProviderId = providerId;
        Sensitivity = sensitivity;
    }

    /// <param name="patient">The patient the encounter is for (existence is checked by the use case, CC-1).</param>
    /// <param name="classCode">Requested class, or blank for the list default (CC-3).</param>
    /// <param name="calendarCategoryId">Legacy <c>pc_catid</c>; required and positive (CC-2).</param>
    /// <param name="date">The encounter date, or <c>null</c> for <paramref name="now"/> (CC-4).</param>
    /// <param name="facilityId">Facility id, or <c>0</c> for none, as legacy's <c>facility_id</c> default.</param>
    /// <param name="posCode">Requested place of service, or <c>null</c> to take the facility's (CC-8).</param>
    /// <param name="providerId">The acting provider; caller-supplied until identity forwarding exists (CC-10, F-CC-1).</param>
    /// <param name="now">The current instant, from the clock (DOM-8).</param>
    /// <param name="reference">Class list and facility read beforehand by the use case.</param>
    /// <exception cref="ClinicalCareValidationException">One or more fields fail validation.</exception>
    public static EncounterCreation Create(
        PatientReference patient, string? classCode, int calendarCategoryId, DateTime? date, string? reason,
        int facilityId, int? posCode, long providerId, DateTime? onsetDate, string? sensitivity,
        DateTime now, EncounterReferenceData reference)
    {
        var errors = new List<string>();

        // The field rules are shared with EncounterChange (one owner per rule, DOM-2).
        var resolved = EncounterFieldRules.Validate(
            classCode, calendarCategoryId, reason, sensitivity, providerId, "ProviderId must identify the acting provider.",
            date, onsetDate, facilityId, posCode, now, reference, errors);

        if (errors.Count > 0)
            throw new ClinicalCareValidationException(errors);

        // CC-4: the UI path stamps a full timestamp (save.php:55-63).
        return new EncounterCreation(
            patient, resolved.ClassCode!, calendarCategoryId, date ?? now, onsetDate, reason,
            facilityId, resolved.FacilityName, resolved.PosCode, providerId, sensitivity);
    }
}
