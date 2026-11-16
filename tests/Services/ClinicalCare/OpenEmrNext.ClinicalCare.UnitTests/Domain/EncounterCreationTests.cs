using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.UnitTests.Domain;

/// <summary>
/// Rules CC-2, CC-3, CC-4, CC-8, CC-10, CC-12, CC-13 and CC-14 (docs/clinical-care/01-create-encounter-slice.md
/// section 3), tested with no database or framework (DOM-19). Rule ids are in the test names (QLT).
/// </summary>
public class EncounterCreationTests
{
    private static readonly DateTime Now = new(2026, 11, 16, 9, 30, 0);

    // The snapshot's _ActEncounterCode list (subset), with AMB as the only default.
    private static readonly IReadOnlyList<EncounterClassOption> Classes =
    [
        new("ACUTE", "Inpatient Acute", false),
        new("AMB", "Outpatient", true),
        new("EMER", "Emergency Dept", false)
    ];

    private static readonly FacilityOption MainClinic = new(3, "Main Clinic", 11);

    private static PatientReference Patient() => PatientReference.Create(Guid.NewGuid(), 42);

    private static EncounterReferenceData Reference(FacilityOption? facility = null) => new(Classes, facility);

    private static EncounterCreation Create(
        string? classCode = "AMB", int calendarCategoryId = 5, DateTime? date = null, string? reason = "Cough",
        int facilityId = 0, int? posCode = null, long providerId = 7, DateTime? onsetDate = null,
        string? sensitivity = null, EncounterReferenceData? reference = null) =>
        EncounterCreation.Create(
            Patient(), classCode, calendarCategoryId, date, reason, facilityId, posCode, providerId,
            onsetDate, sensitivity, Now, reference ?? Reference());

    [Fact]
    public void A_valid_request_creates_the_encounter_facts()
    {
        var creation = Create(classCode: "EMER", reason: "Chest pain", sensitivity: "normal");

        Assert.Equal("EMER", creation.ClassCode);
        Assert.Equal(5, creation.CalendarCategoryId);
        Assert.Equal("Chest pain", creation.Reason);
        Assert.Equal(7, creation.ProviderId);
        Assert.Equal("normal", creation.Sensitivity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void CC_2_a_calendar_category_is_required_and_positive(int category)
    {
        var ex = Assert.Throws<EncounterValidationException>(() => Create(calendarCategoryId: category));

        Assert.Contains("A calendar category is required.", ex.Errors);
    }

    [Fact]
    public void CC_3_an_unknown_class_code_is_rejected()
    {
        var ex = Assert.Throws<EncounterValidationException>(() => Create(classCode: "XYZ"));

        Assert.Contains("Class 'XYZ' is not a known encounter class.", ex.Errors);
    }

    [Fact]
    public void CC_3_class_codes_compare_exactly_not_case_insensitively()
    {
        Assert.Throws<EncounterValidationException>(() => Create(classCode: "amb"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CC_3_a_blank_class_takes_the_list_default(string? blank)
    {
        Assert.Equal("AMB", Create(classCode: blank).ClassCode);
    }

    [Fact]
    public void CC_3_with_no_default_flagged_the_first_entry_is_used()
    {
        var noDefault = new EncounterReferenceData([new("ACUTE", "Inpatient Acute", false), new("AMB", "Outpatient", false)], null);

        Assert.Equal("ACUTE", Create(classCode: null, reference: noDefault).ClassCode);
    }

    [Fact]
    public void CC_3_a_blank_class_with_no_configured_classes_is_an_error_not_a_crash()
    {
        var empty = new EncounterReferenceData([], null);

        var ex = Assert.Throws<EncounterValidationException>(() => Create(classCode: null, reference: empty));

        Assert.Single(ex.Errors);
    }

    [Fact]
    public void CC_4_a_missing_date_is_now_from_the_injected_clock()
    {
        Assert.Equal(Now, Create(date: null).Date);
    }

    [Fact]
    public void CC_4_a_supplied_date_is_kept_including_the_time_of_day()
    {
        var supplied = new DateTime(2026, 11, 1, 14, 5, 0);

        Assert.Equal(supplied, Create(date: supplied).Date);
    }

    [Fact]
    public void CC_8_the_facility_name_and_pos_code_come_from_the_facility()
    {
        var creation = Create(facilityId: 3, reference: Reference(MainClinic));

        Assert.Equal("Main Clinic", creation.FacilityName);
        Assert.Equal(11, creation.PosCode);
    }

    [Fact]
    public void CC_8_a_supplied_pos_code_wins_over_the_facility_default()
    {
        Assert.Equal(21, Create(facilityId: 3, posCode: 21, reference: Reference(MainClinic)).PosCode);
    }

    [Fact]
    public void CC_8_no_facility_leaves_name_and_pos_empty()
    {
        var creation = Create(facilityId: 0);

        Assert.Null(creation.FacilityName);
        Assert.Null(creation.PosCode);
    }

    [Theory]
    [InlineData(99)]
    public void CC_8_an_unknown_facility_is_rejected(int facilityId)
    {
        var ex = Assert.Throws<EncounterValidationException>(() => Create(facilityId: facilityId));

        Assert.Contains("Facility 99 does not exist.", ex.Errors);
    }

    [Fact]
    public void CC_8_a_facility_that_does_not_match_the_requested_id_is_rejected()
    {
        // The use case looked up facility 3, but the request named 4: never accept someone else's row.
        Assert.Throws<EncounterValidationException>(() => Create(facilityId: 4, reference: Reference(MainClinic)));
    }

    [Fact]
    public void CC_8_a_negative_facility_id_is_rejected()
    {
        var ex = Assert.Throws<EncounterValidationException>(() => Create(facilityId: -1));

        Assert.Contains("FacilityId cannot be negative.", ex.Errors);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CC_10_the_acting_provider_must_be_identifiable(long providerId)
    {
        var ex = Assert.Throws<EncounterValidationException>(() => Create(providerId: providerId));

        Assert.Contains("ProviderId must identify the acting provider.", ex.Errors);
    }

    [Fact]
    public void CC_12_a_reason_over_the_bound_is_rejected_and_one_at_the_bound_is_accepted()
    {
        Assert.Equal(EncounterCreation.MaxReasonLength, Create(reason: new string('r', EncounterCreation.MaxReasonLength)).Reason!.Length);
        Assert.Throws<EncounterValidationException>(() => Create(reason: new string('r', EncounterCreation.MaxReasonLength + 1)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(200)]
    public void CC_13_a_pos_code_outside_1_to_99_is_rejected(int pos)
    {
        var ex = Assert.Throws<EncounterValidationException>(() => Create(posCode: pos));

        Assert.Contains($"PosCode must be between 1 and {EncounterCreation.MaxPosCode}.", ex.Errors);
    }

    [Fact]
    public void CC_14_a_sensitivity_over_30_characters_is_rejected()
    {
        Assert.Equal(30, Create(sensitivity: new string('s', 30)).Sensitivity!.Length);
        Assert.Throws<EncounterValidationException>(() => Create(sensitivity: new string('s', 31)));
    }

    [Fact]
    public void Every_violation_is_reported_at_once_not_just_the_first()
    {
        var ex = Assert.Throws<EncounterValidationException>(() =>
            Create(classCode: "XYZ", calendarCategoryId: 0, providerId: 0, facilityId: 99, posCode: 500));

        Assert.Equal(5, ex.Errors.Count);
    }

    [Theory]
    [InlineData("0001-01-01T00:00:00")]
    [InlineData("1899-12-31T23:59:59")]
    [InlineData("2027-11-18T00:00:00")]   // more than a year past the injected "now" (2026-11-16)
    [InlineData("9999-12-31T23:59:59")]
    public void CC_17_an_encounter_or_onset_date_outside_1900_to_a_year_ahead_is_rejected(string text)
    {
        var date = DateTime.Parse(text);

        Assert.Contains("Date must be between 1900-01-01 and one year from now.",
            Assert.Throws<EncounterValidationException>(() => Create(date: date)).Errors);
        Assert.Contains("Onset date must be between 1900-01-01 and one year from now.",
            Assert.Throws<EncounterValidationException>(() => Create(onsetDate: date)).Errors);
    }

    [Theory]
    [InlineData("1900-01-01T00:00:00")]
    [InlineData("1970-06-15T08:00:00")]
    [InlineData("2027-11-16T09:30:00")]   // exactly a year ahead is allowed: a scheduled visit
    public void CC_17_the_edges_of_the_date_range_are_accepted(string text)
    {
        var date = DateTime.Parse(text);

        Assert.Equal(date, Create(date: date).Date);
        Assert.Equal(date, Create(onsetDate: date).OnsetDate);
    }

    [Theory]
    [InlineData(3_000_000_000L)]
    [InlineData(int.MaxValue + 1L)]
    public void R_1_a_provider_id_that_does_not_fit_the_int_column_is_rejected(long providerId)
    {
        Assert.Contains("ProviderId must identify the acting provider.",
            Assert.Throws<EncounterValidationException>(() => Create(providerId: providerId)).Errors);
    }

    [Fact]
    public void R_1_the_largest_provider_id_the_column_holds_is_accepted() =>
        Assert.Equal(int.MaxValue, Create(providerId: int.MaxValue).ProviderId);
}
