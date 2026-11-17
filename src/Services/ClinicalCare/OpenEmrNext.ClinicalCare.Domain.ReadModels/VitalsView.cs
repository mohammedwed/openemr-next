namespace OpenEmrNext.ClinicalCare.Domain.ReadModels;

/// <summary>
/// One set of vitals as a caller reads it (ADR 0054): a query result, not an aggregate. Values are in the **stored**
/// US units; the API converts for display (ADR 0065). Built tolerantly (DOM-9): every column that legacy may have left
/// at its default is nullable here, and a stored <c>0</c> is read as <i>not recorded</i> (CV-12), because the
/// columns default to <c>0.000000</c> and no real measurement is zero.
/// </summary>
/// <param name="Uuid">The public identity.</param>
/// <param name="RecordedAt">When the set was recorded, if the row has a date.</param>
/// <param name="RecordedBy">The username legacy recorded, if any.</param>
/// <param name="WeightPounds">Weight in pounds.</param>
/// <param name="HeightInches">Height in inches.</param>
/// <param name="TemperatureFahrenheit">Temperature in degrees Fahrenheit.</param>
/// <param name="TemperatureMethod">How the temperature was taken, if recorded.</param>
/// <param name="Systolic">Systolic blood pressure, mm[Hg], if the stored text is a whole number.</param>
/// <param name="Diastolic">Diastolic blood pressure, mm[Hg], if the stored text is a whole number.</param>
/// <param name="Pulse">Pulse, per minute.</param>
/// <param name="Respiration">Respiration, per minute.</param>
/// <param name="OxygenSaturation">Oxygen saturation, percent.</param>
/// <param name="WaistInches">Waist circumference in inches.</param>
/// <param name="HeadInches">Head circumference in inches.</param>
/// <param name="Bmi">Body mass index as stored, kg/m².</param>
/// <param name="BmiStatus">The stored BMI category label.</param>
/// <param name="Note">A free-text note.</param>
/// <param name="LastUpdated">When the row last changed, by any writer: the basis of the concurrency token (ADR 0068).</param>
public sealed record VitalsView(
    Guid Uuid,
    DateTime? RecordedAt,
    string? RecordedBy,
    decimal? WeightPounds,
    decimal? HeightInches,
    decimal? TemperatureFahrenheit,
    string? TemperatureMethod,
    int? Systolic,
    int? Diastolic,
    int? Pulse,
    int? Respiration,
    decimal? OxygenSaturation,
    decimal? WaistInches,
    decimal? HeadInches,
    decimal? Bmi,
    string? BmiStatus,
    string? Note,
    DateTime? LastUpdated = null);

/// <summary>One page of an encounter's vitals, newest first, with the total so a caller can page.</summary>
public sealed record VitalsPage(IReadOnlyList<VitalsView> Items, int Total);
