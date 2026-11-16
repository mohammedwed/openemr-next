namespace OpenEmrNext.ClinicalCare.Domain.VitalSigns;

/// <summary>
/// Unit conversion as a pure function (DOM-12, ADR 0065). The factors and the six-decimal rounding are legacy's
/// <c>MeasurementUtils</c> (OpenEMR 8.0.0, <c>src/Common/Utils/MeasurementUtils.php</c>), so a value converted here and
/// one converted by legacy agree to the column's precision.
/// </summary>
public static class UnitConversion
{
    /// <summary>The columns are <c>decimal(12,6)</c>; <c>MeasurementUtils::MEASUREMENT_PRECISION</c> is 6.</summary>
    public const int Precision = 6;

    private const decimal KilogramsToPounds = 2.20462262185m; // MeasurementUtils::kgToLb
    private const decimal PoundsToKilograms = 0.45359237m;    // MeasurementUtils::lbToKg
    private const decimal CentimetersPerInch = 2.54m;         // MeasurementUtils::cmToInches / inchesToCm

    /// <summary>The unit a kind is stored in, always US customary (ADR 0065 #1).</summary>
    public static MeasurementUnit StoredUnit(MeasurementKind kind) => kind switch
    {
        MeasurementKind.Weight => MeasurementUnit.Pound,
        MeasurementKind.Temperature => MeasurementUnit.DegreeFahrenheit,
        _ => MeasurementUnit.Inch
    };

    /// <summary>The unit a kind is shown in for <paramref name="system"/>.</summary>
    public static MeasurementUnit DisplayUnit(MeasurementKind kind, UnitSystem system) => system == UnitSystem.Us
        ? StoredUnit(kind)
        : kind switch
        {
            MeasurementKind.Weight => MeasurementUnit.Kilogram,
            MeasurementKind.Temperature => MeasurementUnit.DegreeCelsius,
            _ => MeasurementUnit.Centimeter
        };

    /// <summary>True when <paramref name="unit"/> is one a caller may use for <paramref name="kind"/>.</summary>
    public static bool IsAcceptable(MeasurementKind kind, MeasurementUnit unit) => kind switch
    {
        MeasurementKind.Weight => unit is MeasurementUnit.Pound or MeasurementUnit.Kilogram,
        MeasurementKind.Temperature => unit is MeasurementUnit.DegreeFahrenheit or MeasurementUnit.DegreeCelsius,
        _ => unit is MeasurementUnit.Inch or MeasurementUnit.Centimeter
    };

    /// <summary>Converts a caller's quantity to the stored unit. The caller has already checked <see cref="IsAcceptable"/>.</summary>
    public static decimal ToStored(MeasurementKind kind, Quantity quantity)
    {
        var stored = (kind, quantity.Unit) switch
        {
            (MeasurementKind.Weight, MeasurementUnit.Kilogram) => quantity.Value * KilogramsToPounds,
            (MeasurementKind.Temperature, MeasurementUnit.DegreeCelsius) => (9m / 5m * quantity.Value) + 32m,
            (_, MeasurementUnit.Centimeter) => quantity.Value / CentimetersPerInch,
            _ => quantity.Value
        };

        return Round(stored);
    }

    /// <summary>Converts a stored (US) value to what a read in <paramref name="system"/> shows.</summary>
    public static decimal FromStored(MeasurementKind kind, decimal stored, UnitSystem system)
    {
        if (system == UnitSystem.Us)
            return stored;

        return Round(kind switch
        {
            MeasurementKind.Weight => stored * PoundsToKilograms,
            MeasurementKind.Temperature => (stored - 32m) * (5m / 9m),
            _ => stored * CentimetersPerInch
        });
    }

    // PHP's round() and number_format() round half away from zero; decimal's default is banker's rounding.
    private static decimal Round(decimal value) => Math.Round(value, Precision, MidpointRounding.AwayFromZero);
}
