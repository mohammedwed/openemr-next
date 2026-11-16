namespace OpenEmrNext.ClinicalCare.Domain.VitalSigns;

/// <summary>Body mass index categories, with legacy's own labels (they are stored in <c>form_vitals.BMI_status</c>).</summary>
public enum BmiCategory
{
    ObesityIII,
    ObesityII,
    ObesityI,
    Overweight,
    NormalBorderline,
    Normal,
    Underweight
}

/// <summary>
/// BMI and its category as pure functions (DOM-12, CV-5, CV-6). Both are legacy's: the formula is in
/// <c>interface/forms/vitals/C_FormVitals.class.php</c> (<c>default_action_process</c>) and the thresholds are
/// <c>Common/Forms/BmiCategory.php</c>, read from OpenEMR 8.0.0.
/// </summary>
public static class BmiRule
{
    /// <summary>Legacy multiplies pounds-per-square-inch by 703 to get kg/m².</summary>
    private const decimal ImperialFactor = 703m;

    /// <summary>
    /// <c>weight / height / height * 703</c>, only when both are above zero; otherwise there is no BMI. Same six-decimal
    /// rounding as the column.
    /// </summary>
    public static decimal? Calculate(decimal? weightPounds, decimal? heightInches) =>
        weightPounds is > 0 && heightInches is > 0
            ? Math.Round(weightPounds.Value / heightInches.Value / heightInches.Value * ImperialFactor,
                UnitConversion.Precision, MidpointRounding.AwayFromZero)
            : null;

    /// <summary>The category for a BMI. Thresholds are strictly greater-than, as in legacy, and a BMI of 10 or less has none.</summary>
    public static BmiCategory? Categorize(decimal bmi) => bmi switch
    {
        > 42m => BmiCategory.ObesityIII,
        > 34m => BmiCategory.ObesityII,
        > 30m => BmiCategory.ObesityI,
        > 27m => BmiCategory.Overweight,
        > 25m => BmiCategory.NormalBorderline,
        > 18.5m => BmiCategory.Normal,
        > 10m => BmiCategory.Underweight,
        _ => null
    };

    /// <summary>The text legacy stores in <c>BMI_status</c> for each category (<c>BmiCategory</c> enum values).</summary>
    public static string Label(BmiCategory category) => category switch
    {
        BmiCategory.ObesityIII => "Obesity III",
        BmiCategory.ObesityII => "Obesity II",
        BmiCategory.ObesityI => "Obesity I",
        BmiCategory.Overweight => "Overweight",
        BmiCategory.NormalBorderline => "Normal BL",
        BmiCategory.Normal => "Normal",
        _ => "Underweight"
    };
}
