using OpenEmrNext.ClinicalCare.Domain.VitalSigns;

namespace OpenEmrNext.ClinicalCare.Api.Endpoints;

/// <summary>
/// The wire spelling of units, kept at the edge (DOM-14): the domain says <c>Kilogram</c>, the API says <c>kg</c>. A new
/// spelling or a code-system mapping is one change here, not a rename across the model.
/// </summary>
internal static class UnitWire
{
    private static readonly Dictionary<string, MeasurementUnit> Parsed = new(StringComparer.Ordinal)
    {
        ["lb"] = MeasurementUnit.Pound,
        ["kg"] = MeasurementUnit.Kilogram,
        ["in"] = MeasurementUnit.Inch,
        ["cm"] = MeasurementUnit.Centimeter,
        ["degF"] = MeasurementUnit.DegreeFahrenheit,
        ["degC"] = MeasurementUnit.DegreeCelsius
    };

    public static string Name(MeasurementUnit unit) => unit switch
    {
        MeasurementUnit.Pound => "lb",
        MeasurementUnit.Kilogram => "kg",
        MeasurementUnit.Inch => "in",
        MeasurementUnit.Centimeter => "cm",
        MeasurementUnit.DegreeFahrenheit => "degF",
        _ => "degC"
    };

    /// <summary>Exact, case-sensitive: <c>KG</c> is not <c>kg</c>. An unknown unit is a 400, never a guess.</summary>
    public static bool TryParse(string? text, out MeasurementUnit unit)
    {
        unit = default;
        return text is not null && Parsed.TryGetValue(text, out unit);
    }

    /// <summary>The unit system named by a <c>?units=</c> query value; <c>null</c> means the default (US).</summary>
    public static bool TryParseSystem(string? text, out UnitSystem system)
    {
        system = UnitSystem.Us;
        switch (text)
        {
            case null or "": return true;
            case "us": return true;
            case "metric": system = UnitSystem.Metric; return true;
            default: return false;
        }
    }
}
