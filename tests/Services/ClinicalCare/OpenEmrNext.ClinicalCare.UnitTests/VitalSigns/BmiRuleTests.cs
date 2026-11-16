using OpenEmrNext.ClinicalCare.Domain.VitalSigns;

namespace OpenEmrNext.ClinicalCare.UnitTests.VitalSigns;

/// <summary>CV-5 and CV-6: legacy's formula and strictly-greater-than thresholds (<c>BmiCategory.php</c>, 8.0.0).</summary>
public class BmiRuleTests
{
    [Fact]
    public void CV_5_bmi_is_pounds_over_inches_squared_times_703()
    {
        // 180 / 70 / 70 * 703 = 25.82448979... -> 25.824490
        Assert.Equal(25.82449m, BmiRule.Calculate(180m, 70m)!.Value, 5);
    }

    [Theory]
    [InlineData(null, 70.0)]
    [InlineData(180.0, null)]
    [InlineData(0.0, 70.0)]
    [InlineData(180.0, 0.0)]
    [InlineData(null, null)]
    public void CV_5_there_is_no_bmi_unless_weight_and_height_are_both_present_and_positive(double? weight, double? height)
    {
        Assert.Null(BmiRule.Calculate((decimal?)weight, (decimal?)height));
    }

    [Theory]
    [InlineData(10, null)]
    [InlineData(10.01, BmiCategory.Underweight)]
    [InlineData(18.5, BmiCategory.Underweight)]
    [InlineData(18.51, BmiCategory.Normal)]
    [InlineData(25, BmiCategory.Normal)]
    [InlineData(25.01, BmiCategory.NormalBorderline)]
    [InlineData(27, BmiCategory.NormalBorderline)]
    [InlineData(27.01, BmiCategory.Overweight)]
    [InlineData(30, BmiCategory.Overweight)]
    [InlineData(30.01, BmiCategory.ObesityI)]
    [InlineData(34, BmiCategory.ObesityI)]
    [InlineData(34.01, BmiCategory.ObesityII)]
    [InlineData(42, BmiCategory.ObesityII)]
    [InlineData(42.01, BmiCategory.ObesityIII)]
    public void CV_6_thresholds_are_strictly_greater_than_exactly_as_legacy_has_them(double bmi, BmiCategory? expected)
    {
        Assert.Equal(expected, BmiRule.Categorize((decimal)bmi));
    }

    [Theory]
    [InlineData(BmiCategory.ObesityIII, "Obesity III")]
    [InlineData(BmiCategory.ObesityII, "Obesity II")]
    [InlineData(BmiCategory.ObesityI, "Obesity I")]
    [InlineData(BmiCategory.Overweight, "Overweight")]
    [InlineData(BmiCategory.NormalBorderline, "Normal BL")]
    [InlineData(BmiCategory.Normal, "Normal")]
    [InlineData(BmiCategory.Underweight, "Underweight")]
    public void The_stored_label_is_legacys_text_not_our_enum_name(BmiCategory category, string label)
    {
        Assert.Equal(label, BmiRule.Label(category));
    }
}
