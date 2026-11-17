using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.UnitTests.Application;

/// <summary>"Did anything change?" has one answer, shared by the event, the audit row and the decision to write (ADR 0068).</summary>
public class EncounterChangeSetTests
{
    private static readonly DateTime Now = new(2026, 11, 17, 9, 30, 0);
    private static readonly DateTime Date = new(2026, 11, 10, 8, 0, 0);
    private static readonly EncounterReferenceData Reference = new(
        [new("AMB", "Outpatient", true), new("EMER", "Emergency Dept", false)], new FacilityOption(3, "Main Clinic", 11));

    private static EncounterView Stored() =>
        new(Guid.NewGuid(), 1001, Guid.NewGuid(), 42, "AMB", Date, new DateTime(2026, 11, 1), "Cough", 3, "Main Clinic", 11, 7, null, null, 5);

    private static EncounterChange Change(
        string classCode = "AMB", int category = 5, DateTime? date = null, DateTime? onset = null, string? reason = "Cough",
        int facilityId = 3, int? pos = 11, long providerId = 7) =>
        EncounterChange.Create(classCode, category, date ?? Date, onset ?? new DateTime(2026, 11, 1), reason, facilityId, pos, providerId, Now, Reference);

    [Fact]
    public void An_identical_change_changes_nothing() =>
        Assert.Empty(EncounterChangeSet.Between(Stored(), Change()));

    [Fact]
    public void Each_field_is_reported_by_its_wire_name_in_a_stable_order()
    {
        var changed = EncounterChangeSet.Between(
            Stored(),
            Change(classCode: "EMER", category: 9, date: Date.AddHours(1), onset: new DateTime(2026, 10, 1), reason: "Other", facilityId: 0, pos: 21, providerId: 8));

        Assert.Equal(["classCode", "calendarCategoryId", "date", "onsetDate", "reason", "facilityId", "posCode", "providerId"], changed);
    }

    [Theory]
    [InlineData("classCode")]
    [InlineData("calendarCategoryId")]
    [InlineData("date")]
    [InlineData("onsetDate")]
    [InlineData("reason")]
    [InlineData("facilityId")]
    [InlineData("posCode")]
    [InlineData("providerId")]
    public void Changing_exactly_one_field_reports_exactly_that_field(string field)
    {
        var change = field switch
        {
            "classCode" => Change(classCode: "EMER"),
            "calendarCategoryId" => Change(category: 9),
            "date" => Change(date: Date.AddDays(1)),
            "onsetDate" => Change(onset: new DateTime(2026, 10, 1)),
            "reason" => Change(reason: "Other"),
            "facilityId" => Change(facilityId: 0, pos: 11),
            "posCode" => Change(pos: 21),
            _ => Change(providerId: 8)
        };

        Assert.Equal([field], EncounterChangeSet.Between(Stored(), change));
    }

    [Fact]
    public void A_null_and_an_empty_reason_are_the_same_reason()
    {
        Assert.Empty(EncounterChangeSet.Between(Stored() with { Reason = null }, Change(reason: "")));
        Assert.Empty(EncounterChangeSet.Between(Stored() with { Reason = "" }, Change(reason: null)));
    }

    [Fact]
    public void Clearing_a_reason_is_a_change() =>
        Assert.Equal(["reason"], EncounterChangeSet.Between(Stored(), Change(reason: null)));

    [Fact]
    public void A_stored_encounter_with_no_date_or_category_differs_from_a_change_that_has_them()
    {
        var changed = EncounterChangeSet.Between(Stored() with { Date = null, CalendarCategoryId = null }, Change());

        Assert.Equal(["calendarCategoryId", "date"], changed);
    }
}
