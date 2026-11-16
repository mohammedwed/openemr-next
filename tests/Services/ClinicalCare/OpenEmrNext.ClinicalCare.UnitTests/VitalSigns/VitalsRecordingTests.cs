using OpenEmrNext.ClinicalCare.Domain.ValueObjects;
using OpenEmrNext.ClinicalCare.Domain.VitalSigns;

namespace OpenEmrNext.ClinicalCare.UnitTests.VitalSigns;

/// <summary>Rules CV-2 to CV-9 (docs/clinical-care/02-record-vitals-slice.md), with no database or framework (DOM-19).</summary>
public class VitalsRecordingTests
{
    private static readonly DateTime Now = new(2026, 11, 16, 9, 30, 0);
    private static readonly EncounterReference Encounter =
        EncounterReference.Create(Guid.NewGuid(), 1001, Guid.NewGuid(), 42);

    private static VitalsRecording Create(VitalsInput input, long providerId = 7) =>
        VitalsRecording.Create(Encounter, input, providerId, Now);

    private static Quantity Kg(decimal v) => new(v, MeasurementUnit.Kilogram);
    private static Quantity Lb(decimal v) => new(v, MeasurementUnit.Pound);
    private static Quantity Cm(decimal v) => new(v, MeasurementUnit.Centimeter);
    private static Quantity In(decimal v) => new(v, MeasurementUnit.Inch);

    [Fact]
    public void CV_4_metric_input_is_converted_to_the_stored_us_units()
    {
        var recording = Create(new VitalsInput(
            Weight: Kg(70m), Height: Cm(180m), Temperature: new Quantity(37m, MeasurementUnit.DegreeCelsius),
            WaistCircumference: Cm(90m), HeadCircumference: Cm(55m)));

        Assert.Equal(154.323584m, recording.WeightPounds);
        Assert.Equal(70.866142m, recording.HeightInches);
        Assert.Equal(98.6m, recording.TemperatureFahrenheit);
        Assert.Equal(35.433071m, recording.WaistInches); // 90 / 2.54 = 35.4330708...
        Assert.Equal(21.653543m, recording.HeadInches);  // 55 / 2.54 = 21.6535433...
    }

    [Fact]
    public void CV_4_us_input_is_stored_as_given()
    {
        var recording = Create(new VitalsInput(Weight: Lb(154m), Height: In(70m)));

        Assert.Equal(154m, recording.WeightPounds);
        Assert.Equal(70m, recording.HeightInches);
    }

    [Fact]
    public void CV_5_bmi_and_status_are_derived_from_the_stored_values()
    {
        var recording = Create(new VitalsInput(Weight: Lb(180m), Height: In(70m)));

        Assert.Equal(25.82449m, recording.Bmi!.Value, 5);
        Assert.Equal("Normal BL", recording.BmiStatus);
    }

    [Fact]
    public void CV_5_bmi_comes_from_metric_input_too_via_the_stored_units()
    {
        // 70 kg -> 154.323584 lb and 175 cm -> 68.897638 in, then legacy's formula: 22.854881 (worked out independently).
        // The pure metric value is 70 / 1.75^2 = 22.857143. The 0.0023 gap is legacy's rounded 703 constant; this is
        // parity, not an error, so the exact figure is asserted.
        var recording = Create(new VitalsInput(Weight: Kg(70m), Height: Cm(175m)));

        Assert.Equal(22.854881m, recording.Bmi);
        Assert.Equal("Normal", recording.BmiStatus);
    }

    [Fact]
    public void CV_5_weight_alone_has_no_bmi_and_no_status()
    {
        var recording = Create(new VitalsInput(Weight: Kg(70m)));

        Assert.Null(recording.Bmi);
        Assert.Null(recording.BmiStatus);
    }

    [Fact]
    public void CV_2_an_empty_recording_is_rejected()
    {
        var ex = Assert.Throws<VitalsValidationException>(() => Create(new VitalsInput()));

        Assert.Contains("At least one measurement is required.", ex.Errors);
    }

    [Fact]
    public void CV_2_a_note_alone_is_not_a_measurement()
    {
        Assert.Throws<VitalsValidationException>(() => Create(new VitalsInput(Note: "patient was calm")));
    }

    [Fact]
    public void CV_4_a_unit_for_the_wrong_quantity_is_rejected_not_guessed()
    {
        var ex = Assert.Throws<VitalsValidationException>(() => Create(new VitalsInput(Weight: Cm(70m))));

        Assert.Contains("Weight must be given in lb or kg.", ex.Errors);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1501)]
    public void CV_3_weight_outside_the_bound_is_rejected(double pounds)
    {
        Assert.Throws<VitalsValidationException>(() => Create(new VitalsInput(Weight: Lb((decimal)pounds))));
    }

    [Fact]
    public void CV_3_the_bound_is_checked_after_conversion_so_a_kilogram_typo_in_pounds_territory_is_caught()
    {
        // 700 kg is 1543 lb: a plausible slip of a decimal point, over the 1500 lb bound.
        Assert.Throws<VitalsValidationException>(() => Create(new VitalsInput(Weight: Kg(700m))));
        Assert.NotNull(Create(new VitalsInput(Weight: Kg(680m))));
    }

    [Theory]
    [InlineData(49.9, false)]
    [InlineData(50, true)]
    [InlineData(120, true)]
    [InlineData(120.1, false)]
    public void CV_3_temperature_bounds_in_fahrenheit_are_inclusive(double degrees, bool ok)
    {
        var input = new VitalsInput(Temperature: new Quantity((decimal)degrees, MeasurementUnit.DegreeFahrenheit));

        if (ok)
            Assert.Equal((decimal)degrees, Create(input).TemperatureFahrenheit);
        else
            Assert.Throws<VitalsValidationException>(() => Create(input));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(400, true)]
    [InlineData(401, false)]
    public void CV_8_blood_pressure_is_a_whole_number_between_1_and_400(int value, bool ok)
    {
        var input = new VitalsInput(Systolic: value);

        if (ok)
            Assert.Equal(value, Create(input).Systolic);
        else
            Assert.Throws<VitalsValidationException>(() => Create(input));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(100, true)]
    [InlineData(100.01, false)]
    public void CV_3_oxygen_saturation_is_above_0_and_at_most_100(double percent, bool ok)
    {
        var input = new VitalsInput(OxygenSaturation: (decimal)percent);

        if (ok)
            Assert.Equal((decimal)percent, Create(input).OxygenSaturation);
        else
            Assert.Throws<VitalsValidationException>(() => Create(input));
    }

    [Fact]
    public void CV_7_a_temperature_method_is_kept_only_with_a_temperature()
    {
        var withTemp = Create(new VitalsInput(
            Temperature: new Quantity(98.6m, MeasurementUnit.DegreeFahrenheit), TemperatureMethod: "Oral", Pulse: 70));
        var without = Create(new VitalsInput(TemperatureMethod: "Oral", Pulse: 70));

        Assert.Equal("Oral", withTemp.TemperatureMethod);
        Assert.Null(without.TemperatureMethod);
    }

    [Fact]
    public void CV_13_the_measurements_taken_are_named_and_a_rejected_one_is_not_listed()
    {
        var recording = Create(new VitalsInput(Weight: Kg(70m), Systolic: 120, Diastolic: 80, Pulse: 64, Note: "calm"));

        Assert.Equal(["weight", "bloodPressureSystolic", "bloodPressureDiastolic", "pulse"], recording.MeasurementsTaken);
    }

    [Fact]
    public void CV_9_the_record_is_stamped_with_the_injected_clock()
    {
        Assert.Equal(Now, Create(new VitalsInput(Pulse: 64)).RecordedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void The_acting_provider_must_be_identifiable(long providerId)
    {
        var ex = Assert.Throws<VitalsValidationException>(() => Create(new VitalsInput(Pulse: 64), providerId));

        Assert.Contains("ProviderId must identify the acting provider.", ex.Errors);
    }

    [Fact]
    public void A_note_over_255_characters_is_rejected_and_one_at_255_is_kept()
    {
        Assert.Equal(255, Create(new VitalsInput(Pulse: 64, Note: new string('n', 255))).Note!.Length);
        Assert.Throws<VitalsValidationException>(() => Create(new VitalsInput(Pulse: 64, Note: new string('n', 256))));
    }

    [Fact]
    public void Every_violation_is_reported_at_once_not_just_the_first()
    {
        var ex = Assert.Throws<VitalsValidationException>(() => Create(new VitalsInput(
            Weight: Cm(70m), Height: In(500m), Systolic: 0, Pulse: 999, OxygenSaturation: 101m), providerId: 0));

        Assert.Equal(6, ex.Errors.Count);
    }

    [Fact]
    public void EncounterReference_needs_a_number_and_a_known_patient()
    {
        var ex = Assert.Throws<VitalsValidationException>(() => EncounterReference.Create(Guid.NewGuid(), null, null, null));

        Assert.Equal(2, ex.Errors.Count);
    }

    [Fact]
    public void R_3_a_number_big_enough_to_overflow_decimal_during_conversion_is_rejected_not_thrown_as_an_overflow()
    {
        // 7e28 kg * 2.2046 exceeds decimal.MaxValue (7.9e28): before the fix this was an OverflowException, a 500.
        var ex = Assert.Throws<VitalsValidationException>(() => Create(new VitalsInput(Weight: Kg(70_000_000_000_000_000_000_000_000_000m))));

        Assert.Contains("Weight must be between 0.1 and 1500 lb.", ex.Errors);
    }

    [Theory]
    [InlineData(1_000_001)]
    [InlineData(-1_000_001)]
    public void R_3_anything_beyond_the_raw_cap_is_out_of_bounds_in_every_unit(double value)
    {
        Assert.Throws<VitalsValidationException>(() => Create(new VitalsInput(Height: Cm((decimal)value))));
        Assert.Throws<VitalsValidationException>(() => Create(new VitalsInput(Temperature: new Quantity((decimal)value, MeasurementUnit.DegreeCelsius))));
    }

    [Theory]
    [InlineData(4.99, false)]
    [InlineData(5, true)]
    [InlineData(120, true)]
    public void R_2_height_has_a_floor_so_the_derived_bmi_always_fits_its_column(double inches, bool ok)
    {
        var input = new VitalsInput(Weight: Lb(1500m), Height: In((decimal)inches));

        if (ok)
            Assert.True(Create(input).Bmi < 999_999m);
        else
            Assert.Throws<VitalsValidationException>(() => Create(input));
    }

    [Fact]
    public void R_2_the_worst_case_bmi_the_bounds_allow_fits_decimal_12_6()
    {
        // The heaviest allowed weight over the shortest allowed height is the largest BMI any accepted input can produce.
        var worst = Create(new VitalsInput(Weight: Lb(VitalsRecording.MaxWeightPounds), Height: In(VitalsRecording.MinHeightInches)));

        Assert.True(worst.Bmi < 999_999.999999m);
        Assert.Equal(42_180m, worst.Bmi);   // 1500 / 5 / 5 * 703
    }

    [Theory]
    [InlineData(0.09, false)]
    [InlineData(0.1, true)]
    public void R_2_weight_has_a_floor(double pounds, bool ok)
    {
        var input = new VitalsInput(Weight: Lb((decimal)pounds));

        if (ok)
            Assert.Equal(0.1m, Create(input).WeightPounds);
        else
            Assert.Throws<VitalsValidationException>(() => Create(input));
    }

    [Theory]
    [InlineData(3_000_000_000L)]
    [InlineData(int.MaxValue + 1L)]
    public void R_1_a_vitals_provider_id_that_does_not_fit_the_users_id_column_is_rejected(long providerId)
    {
        Assert.Throws<VitalsValidationException>(() => Create(new VitalsInput(Pulse: 64), providerId));
    }

    [Fact]
    public void Oxygen_saturation_is_rounded_to_the_two_places_the_column_stores_so_what_is_returned_is_what_is_held()
    {
        Assert.Equal(98.12m, Create(new VitalsInput(OxygenSaturation: 98.123456m)).OxygenSaturation);
        Assert.Equal(98.13m, Create(new VitalsInput(OxygenSaturation: 98.125m)).OxygenSaturation);   // half away from zero, as the column rounds
        Assert.Equal(100m, Create(new VitalsInput(OxygenSaturation: 99.996m)).OxygenSaturation);
    }
}
