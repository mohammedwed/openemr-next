using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.VitalSigns;

namespace OpenEmrNext.ClinicalCare.UnitTests.VitalSigns;

/// <summary>"Did anything change?" has one answer for a vitals correction, shared by the event, the audit row and the decision to write (ADR 0068).</summary>
public class VitalsChangeSetTests
{
    private static VitalsView Stored() =>
        new(Guid.NewGuid(), null, "dr_who", 154.323584m, 68.897638m, 98.6m, "Oral", 120, 80, 64, 16, 98.13m, 31.496063m, 22.5m, 22.85m, "Normal", "calm", null);

    private static VitalsChange Change(
        decimal weight = 154.323584m, decimal height = 68.897638m, decimal temperature = 98.6m, string? method = "Oral", int? systolic = 120, int? diastolic = 80,
        int? pulse = 64, int? respiration = 16, decimal? oxygen = 98.13m, decimal waist = 31.496063m, decimal head = 22.5m, string? note = "calm") =>
        VitalsChange.Create(new VitalsInput(
            new Quantity(weight, MeasurementUnit.Pound), new Quantity(height, MeasurementUnit.Inch), new Quantity(temperature, MeasurementUnit.DegreeFahrenheit), method,
            systolic, diastolic, pulse, respiration, oxygen, new Quantity(waist, MeasurementUnit.Inch), new Quantity(head, MeasurementUnit.Inch), note));

    [Fact]
    public void An_identical_correction_changes_nothing() =>
        Assert.Empty(VitalsChangeSet.Between(Stored(), Change()));

    [Theory]
    [InlineData("weight")]
    [InlineData("height")]
    [InlineData("temperature")]
    [InlineData("temperatureMethod")]
    [InlineData("bloodPressureSystolic")]
    [InlineData("bloodPressureDiastolic")]
    [InlineData("pulse")]
    [InlineData("respiration")]
    [InlineData("oxygenSaturation")]
    [InlineData("waistCircumference")]
    [InlineData("headCircumference")]
    [InlineData("note")]
    public void Changing_exactly_one_measurement_reports_exactly_that_one(string name)
    {
        var change = name switch
        {
            "weight" => Change(weight: 160m),
            "height" => Change(height: 70m),
            "temperature" => Change(temperature: 99.1m),
            "temperatureMethod" => Change(method: "Axillary"),
            "bloodPressureSystolic" => Change(systolic: 130),
            "bloodPressureDiastolic" => Change(diastolic: 85),
            "pulse" => Change(pulse: 70),
            "respiration" => Change(respiration: 18),
            "oxygenSaturation" => Change(oxygen: 97m),
            "waistCircumference" => Change(waist: 33m),
            "headCircumference" => Change(head: 23m),
            _ => Change(note: "rechecked")
        };

        // Weight and height also move the derived BMI, which is deliberately not listed as a change of its own.
        Assert.Equal([name], VitalsChangeSet.Between(Stored(), change));
    }

    [Fact]
    public void A_removed_reading_is_a_change_and_a_null_and_an_empty_note_are_the_same_note()
    {
        var withoutPulse = Change(pulse: null);

        Assert.Equal(["pulse"], VitalsChangeSet.Between(Stored(), withoutPulse));
        Assert.Empty(VitalsChangeSet.Between(Stored() with { Note = null, TemperatureMethod = "" }, Change(note: "", method: null)));
    }

    [Fact]
    public void A_stored_reading_legacy_holds_as_free_text_reads_as_absent_so_a_numeric_correction_is_a_change()
    {
        // The view reports an unparseable blood pressure as absent (DOM-9); correcting it to a number is therefore a change of that measurement.
        var legacyText = Stored() with { Systolic = null };

        Assert.Equal(["bloodPressureSystolic"], VitalsChangeSet.Between(legacyText, Change()));
    }
}
