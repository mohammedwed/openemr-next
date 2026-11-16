using OpenEmrNext.ClinicalCare.Domain.VitalSigns;

namespace OpenEmrNext.ClinicalCare.UnitTests.VitalSigns;

/// <summary>
/// CV-4 and ADR 0065. Expected values are worked out by hand from legacy's own factors
/// (<c>MeasurementUtils</c>, OpenEMR 8.0.0), not produced by the code under test.
/// </summary>
public class UnitConversionTests
{
    [Fact]
    public void Kilograms_convert_to_pounds_with_legacys_factor_rounded_to_six_places()
    {
        // 70 * 2.20462262185 = 154.3235835295 -> 154.323584
        Assert.Equal(154.323584m, UnitConversion.ToStored(MeasurementKind.Weight, new Quantity(70m, MeasurementUnit.Kilogram)));
    }

    [Fact]
    public void Centimeters_convert_to_inches()
    {
        // 180 / 2.54 = 70.86614173... -> 70.866142
        Assert.Equal(70.866142m, UnitConversion.ToStored(MeasurementKind.Height, new Quantity(180m, MeasurementUnit.Centimeter)));
    }

    [Fact]
    public void Celsius_converts_to_fahrenheit()
    {
        Assert.Equal(98.6m, UnitConversion.ToStored(MeasurementKind.Temperature, new Quantity(37m, MeasurementUnit.DegreeCelsius)));
        Assert.Equal(32m, UnitConversion.ToStored(MeasurementKind.Temperature, new Quantity(0m, MeasurementUnit.DegreeCelsius)));
        Assert.Equal(-40m, UnitConversion.ToStored(MeasurementKind.Temperature, new Quantity(-40m, MeasurementUnit.DegreeCelsius)));
    }

    [Theory]
    [InlineData(MeasurementKind.Weight, MeasurementUnit.Pound)]
    [InlineData(MeasurementKind.Height, MeasurementUnit.Inch)]
    [InlineData(MeasurementKind.Temperature, MeasurementUnit.DegreeFahrenheit)]
    [InlineData(MeasurementKind.WaistCircumference, MeasurementUnit.Inch)]
    [InlineData(MeasurementKind.HeadCircumference, MeasurementUnit.Inch)]
    public void A_value_already_in_the_stored_unit_is_unchanged(MeasurementKind kind, MeasurementUnit unit)
    {
        Assert.Equal(123.456789m, UnitConversion.ToStored(kind, new Quantity(123.456789m, unit)));
        Assert.Equal(unit, UnitConversion.StoredUnit(kind));
    }

    [Fact]
    public void Stored_values_read_back_in_metric_with_legacys_factors()
    {
        // 154.323584 * 0.45359237 = 69.99999...; checked to the column's precision, not equality.
        Assert.Equal(70m, UnitConversion.FromStored(MeasurementKind.Weight, 154.323584m, UnitSystem.Metric), 4);
        Assert.Equal(180m, UnitConversion.FromStored(MeasurementKind.Height, 70.866142m, UnitSystem.Metric), 4);
        Assert.Equal(37m, UnitConversion.FromStored(MeasurementKind.Temperature, 98.6m, UnitSystem.Metric), 4);
    }

    [Fact]
    public void The_us_system_returns_the_stored_value_untouched()
    {
        Assert.Equal(154.323584m, UnitConversion.FromStored(MeasurementKind.Weight, 154.323584m, UnitSystem.Us));
    }

    [Fact]
    public void The_display_unit_follows_the_system()
    {
        Assert.Equal(MeasurementUnit.Kilogram, UnitConversion.DisplayUnit(MeasurementKind.Weight, UnitSystem.Metric));
        Assert.Equal(MeasurementUnit.DegreeCelsius, UnitConversion.DisplayUnit(MeasurementKind.Temperature, UnitSystem.Metric));
        Assert.Equal(MeasurementUnit.Centimeter, UnitConversion.DisplayUnit(MeasurementKind.HeadCircumference, UnitSystem.Metric));
        Assert.Equal(MeasurementUnit.Pound, UnitConversion.DisplayUnit(MeasurementKind.Weight, UnitSystem.Us));
    }

    [Theory]
    [InlineData(MeasurementKind.Weight, MeasurementUnit.Centimeter, false)]
    [InlineData(MeasurementKind.Weight, MeasurementUnit.Pound, true)]
    [InlineData(MeasurementKind.Weight, MeasurementUnit.Kilogram, true)]
    [InlineData(MeasurementKind.Height, MeasurementUnit.Kilogram, false)]
    [InlineData(MeasurementKind.Height, MeasurementUnit.Centimeter, true)]
    [InlineData(MeasurementKind.Temperature, MeasurementUnit.Inch, false)]
    [InlineData(MeasurementKind.Temperature, MeasurementUnit.DegreeCelsius, true)]
    [InlineData(MeasurementKind.WaistCircumference, MeasurementUnit.DegreeFahrenheit, false)]
    public void A_unit_is_acceptable_only_for_the_quantity_it_measures(MeasurementKind kind, MeasurementUnit unit, bool expected)
    {
        Assert.Equal(expected, UnitConversion.IsAcceptable(kind, unit));
    }

    [Fact]
    public void Rounding_is_half_away_from_zero_like_phps_round_not_bankers_rounding()
    {
        // 0.0000005 lb is exactly half of the sixth place: PHP's round() gives 0.000001; banker's rounding gives 0.
        Assert.Equal(0.000001m, UnitConversion.ToStored(MeasurementKind.Weight, new Quantity(0.0000005m, MeasurementUnit.Pound)));
    }
}
