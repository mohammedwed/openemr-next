using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.UnitTests.Domain;

/// <summary>
/// The rules of an encounter update (M5b phase 2, ADR 0068), tested with no database or framework (DOM-19). They share their field rules with
/// creation, so most cases here prove the same rule fires for an update; the rest are what an update does differently: nothing defaults.
/// </summary>
public class EncounterChangeTests
{
    private static readonly DateTime Now = new(2026, 11, 17, 9, 30, 0);

    private static readonly IReadOnlyList<EncounterClassOption> Classes =
    [
        new("ACUTE", "Inpatient Acute", false),
        new("AMB", "Outpatient", true),
        new("EMER", "Emergency Dept", false)
    ];

    private static readonly FacilityOption MainClinic = new(3, "Main Clinic", 11);

    private static EncounterReferenceData Reference(FacilityOption? facility = null) => new(Classes, facility);

    private static EncounterChange Change(
        string? classCode = "AMB", int category = 5, DateTime? date = null, DateTime? onset = null, string? reason = "Cough",
        int facilityId = 0, int? pos = null, long providerId = 7, EncounterReferenceData? reference = null) =>
        EncounterChange.Create(classCode, category, date ?? new DateTime(2026, 11, 10, 8, 0, 0), onset, reason, facilityId, pos, providerId, Now, reference ?? Reference());

    private static IReadOnlyList<string> ErrorsOf(Action act) =>
        Assert.Throws<ClinicalCareValidationException>(act).Errors;

    [Fact]
    public void A_valid_change_keeps_every_editable_field()
    {
        var change = Change(
            classCode: "EMER", category: 9, date: new DateTime(2026, 11, 10, 8, 15, 30), onset: new DateTime(2026, 11, 1), reason: "Chest pain",
            facilityId: 3, pos: 21, providerId: 12, reference: Reference(MainClinic));

        Assert.Equal("EMER", change.ClassCode);
        Assert.Equal(9, change.CalendarCategoryId);
        Assert.Equal(new DateTime(2026, 11, 10, 8, 15, 30), change.Date);
        Assert.Equal(new DateTime(2026, 11, 1), change.OnsetDate);
        Assert.Equal("Chest pain", change.Reason);
        Assert.Equal(3, change.FacilityId);
        Assert.Equal("Main Clinic", change.FacilityName);
        Assert.Equal(21, change.PosCode);
        Assert.Equal(12, change.ProviderId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void A_blank_class_is_an_error_not_the_list_default_because_an_update_never_fills_a_field(string? blank)
    {
        var errors = ErrorsOf(() => Change(classCode: blank));

        Assert.Contains("A class is required.", errors);
    }

    [Fact]
    public void A_missing_date_is_an_error_not_now()
    {
        var errors = ErrorsOf(() => EncounterChange.Create("AMB", 5, null, null, "x", 0, null, 7, Now, Reference()));

        Assert.Contains("A date is required.", errors);
    }

    [Fact]
    public void An_unknown_class_is_rejected_by_the_same_rule_as_a_create() =>
        Assert.Contains("Class 'NOPE' is not a known encounter class.", ErrorsOf(() => Change(classCode: "NOPE")));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_calendar_category_is_required(int category) =>
        Assert.Contains("A calendar category is required.", ErrorsOf(() => Change(category: category)));

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData((long)int.MaxValue + 1)]
    public void The_provider_must_fit_the_column_and_the_message_is_about_the_encounters_provider(long providerId) =>
        Assert.Contains("ProviderId must identify the encounter's provider.", ErrorsOf(() => Change(providerId: providerId)));

    [Fact]
    public void A_date_outside_1900_and_a_year_ahead_is_rejected()
    {
        Assert.Contains("Date must be between 1900-01-01 and one year from now.",
            ErrorsOf(() => Change(date: new DateTime(1800, 1, 1))));
        Assert.Contains("Date must be between 1900-01-01 and one year from now.",
            ErrorsOf(() => Change(date: Now.AddDays(400))));
        Assert.Contains("Onset date must be between 1900-01-01 and one year from now.",
            ErrorsOf(() => Change(onset: new DateTime(1, 1, 1))));
    }

    [Fact]
    public void Reason_longer_than_the_limit_is_rejected() =>
        Assert.Contains($"Reason must be at most {EncounterCreation.MaxReasonLength} characters.",
            ErrorsOf(() => Change(reason: new string('x', EncounterCreation.MaxReasonLength + 1))));

    [Fact]
    public void An_unknown_facility_is_rejected_and_a_negative_one_too()
    {
        Assert.Contains("Facility 9 does not exist.", ErrorsOf(() => Change(facilityId: 9)));
        Assert.Contains("FacilityId cannot be negative.", ErrorsOf(() => Change(facilityId: -1)));
    }

    [Fact]
    public void A_facility_supplies_its_name_and_its_place_of_service_when_none_is_given()
    {
        var change = Change(facilityId: 3, pos: null, reference: Reference(MainClinic));

        Assert.Equal("Main Clinic", change.FacilityName);
        Assert.Equal(11, change.PosCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void A_place_of_service_outside_1_to_99_is_rejected(int pos) =>
        Assert.Contains("PosCode must be between 1 and 99.", ErrorsOf(() => Change(pos: pos)));

    [Fact]
    public void Every_violation_is_reported_together()
    {
        var errors = ErrorsOf(() => EncounterChange.Create(" ", 0, null, null, null, -1, 500, 0, Now, Reference()));

        Assert.True(errors.Count >= 6, string.Join(" | ", errors));
    }

    [Fact]
    public void Dates_are_cut_to_whole_seconds_so_an_unchanged_date_with_a_stray_fraction_is_not_a_change()
    {
        var withFraction = new DateTime(2026, 11, 10, 8, 15, 30).AddTicks(7_654_321);

        var change = Change(date: withFraction, onset: withFraction);

        Assert.Equal(new DateTime(2026, 11, 10, 8, 15, 30), change.Date);
        Assert.Equal(new DateTime(2026, 11, 10, 8, 15, 30), change.OnsetDate);
    }
}
