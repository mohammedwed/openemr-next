namespace OpenEmrNext.ClinicalCare.Domain.VitalSigns;

/// <summary>
/// A unit a caller may state a measurement in (ADR 0065). Named for the concept, not the wire spelling: the API
/// translates <c>kg</c>, <c>degC</c> and the rest (DOM-14).
/// </summary>
public enum MeasurementUnit
{
    Pound,
    Kilogram,
    Inch,
    Centimeter,
    DegreeFahrenheit,
    DegreeCelsius
}

/// <summary>Which quantity a measurement is, which decides the units it may be given in and the unit it is stored in.</summary>
public enum MeasurementKind
{
    Weight,
    Height,
    Temperature,
    WaistCircumference,
    HeadCircumference
}

/// <summary>The unit system a read is shown in. Storage is always <see cref="UnitSystem.Us"/> (ADR 0065).</summary>
public enum UnitSystem
{
    Us,
    Metric
}

/// <summary>A number and the unit it is stated in. No invariant of its own: the bounds depend on the <see cref="MeasurementKind"/>.</summary>
public sealed record Quantity(decimal Value, MeasurementUnit Unit);
