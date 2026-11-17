using OpenEmrNext.ClinicalCare.Domain.ValueObjects;
using OpenEmrNext.ClinicalCare.Domain.VitalSigns;

namespace OpenEmrNext.ClinicalCare.UnitTests.VitalSigns;

/// <summary>
/// The rules of a vitals correction (M5d slice 1, ADR 0068), tested with no database or framework (DOM-19). A correction shares its measurement rules with recording, so most cases here prove the
/// same rule fires for a correction; the rest are what a correction does differently: no actor, and a reading can be removed.
/// </summary>
public class VitalsChangeTests
{
    private static readonly EncounterReference Encounter = EncounterReference.Create(Guid.NewGuid(), 1001, Guid.NewGuid(), 42);
    private static readonly DateTime Now = new(2026, 11, 17, 9, 30, 0);

    private static VitalsChange Change(VitalsInput input) => VitalsChange.Create(input);

    private static IReadOnlyList<string> ErrorsOf(Action act) => Assert.Throws<VitalsValidationException>(act).Errors;

    [Fact]
    public void A_valid_correction_keeps_every_measurement_in_the_stored_us_units_and_derives_the_bmi()
    {
        var change = Change(new VitalsInput(
            Weight: new Quantity(70m, MeasurementUnit.Kilogram), Height: new Quantity(175m, MeasurementUnit.Centimeter),
            Temperature: new Quantity(37m, MeasurementUnit.DegreeCelsius), TemperatureMethod: "Oral", Systolic: 120, Diastolic: 80, Pulse: 64,
            Respiration: 16, OxygenSaturation: 98.126m, WaistCircumference: new Quantity(80m, MeasurementUnit.Centimeter), Note: "calm"));

        Assert.Equal(154.323584m, change.WeightPounds);
        Assert.Equal(68.897638m, change.HeightInches);
        Assert.Equal(98.6m, change.TemperatureFahrenheit);
        Assert.Equal("Oral", change.TemperatureMethod);
        Assert.Equal(120, change.Systolic);
        Assert.Equal(80, change.Diastolic);
        Assert.Equal(64, change.Pulse);
        Assert.Equal(16, change.Respiration);
        Assert.Equal(98.13m, change.OxygenSaturation);       // rounded to the column's two places
        Assert.Equal(31.496063m, change.WaistInches);
        Assert.Equal("calm", change.Note);
        Assert.NotNull(change.Bmi);
        Assert.NotNull(change.BmiStatus);
    }

    [Fact]
    public void A_correction_accepts_exactly_what_a_recording_accepts_and_refuses_exactly_what_it_refuses()
    {
        // The same input through both factories: the same values, or the same messages.
        var inputs = new[]
        {
            new VitalsInput(Weight: new Quantity(70m, MeasurementUnit.Kilogram), Pulse: 64),
            new VitalsInput(Weight: new Quantity(0m, MeasurementUnit.Kilogram)),
            new VitalsInput(Height: new Quantity(1m, MeasurementUnit.Inch), Systolic: 500),
            new VitalsInput(Temperature: new Quantity(200m, MeasurementUnit.DegreeFahrenheit), OxygenSaturation: 101m),
            new VitalsInput(Weight: new Quantity(7e28m, MeasurementUnit.Kilogram)),
            new VitalsInput(Note: new string('x', 300)),
            new VitalsInput()
        };

        foreach (var input in inputs)
        {
            string[] recorded, corrected;
            try { VitalsRecording.Create(Encounter, input, 7, Now); recorded = []; }
            catch (VitalsValidationException ex) { recorded = [.. ex.Errors]; }
            try { VitalsChange.Create(input); corrected = []; }
            catch (VitalsValidationException ex) { corrected = [.. ex.Errors]; }

            Assert.Equal(recorded, corrected);
        }
    }

    [Fact]
    public void A_correction_needs_at_least_one_measurement_so_it_cannot_empty_the_record() =>
        Assert.Contains("At least one measurement is required.", ErrorsOf(() => Change(new VitalsInput(Note: null))));

    [Fact]
    public void A_correction_can_remove_a_reading_by_leaving_it_out()
    {
        var change = Change(new VitalsInput(Pulse: 64));

        Assert.Null(change.WeightPounds);
        Assert.Null(change.Systolic);
        Assert.Null(change.Bmi);
        Assert.Null(change.BmiStatus);
        Assert.Equal(64, change.Pulse);
    }

    [Fact]
    public void A_correction_names_no_provider_so_there_is_no_provider_rule()
    {
        // Unlike a recording, who is making the correction is not a field (ADR 0069 #5): nothing in the change can be refused for lacking one.
        var errors = ErrorsOf(() => Change(new VitalsInput()));

        Assert.DoesNotContain(errors, e => e.Contains("ProviderId", StringComparison.Ordinal));
    }

    [Fact]
    public void The_temperature_method_is_dropped_when_there_is_no_temperature_as_legacy_does()
    {
        var change = Change(new VitalsInput(Pulse: 64, TemperatureMethod: "Oral"));

        Assert.Null(change.TemperatureMethod);
    }

    [Fact]
    public void The_bmi_is_derived_from_the_corrected_weight_and_height_and_cleared_when_either_is_removed()
    {
        var both = Change(new VitalsInput(Weight: new Quantity(70m, MeasurementUnit.Kilogram), Height: new Quantity(175m, MeasurementUnit.Centimeter)));
        var weightOnly = Change(new VitalsInput(Weight: new Quantity(70m, MeasurementUnit.Kilogram)));

        Assert.NotNull(both.Bmi);
        Assert.Null(weightOnly.Bmi);
        Assert.Null(weightOnly.BmiStatus);
    }

    [Fact]
    public void Every_violation_is_reported_together()
    {
        var errors = ErrorsOf(() => Change(new VitalsInput(
            Weight: new Quantity(0m, MeasurementUnit.Pound), Height: new Quantity(1m, MeasurementUnit.Inch), OxygenSaturation: 0m, Note: new string('x', 300))));

        Assert.True(errors.Count >= 4, string.Join(" | ", errors));
    }
}
