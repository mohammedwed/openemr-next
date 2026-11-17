using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Domain.VitalSigns;

/// <summary>
/// The measurement rules both <see cref="VitalsRecording"/> and <see cref="VitalsChange"/> obey (CV-2 to CV-7), written once so recording vitals and correcting them can never disagree
/// about what is valid (one owner per rule, DOM-2): unit acceptance and conversion to the stored US units (ADR 0065), the plausibility bounds, the whole-number readings, the oxygen
/// saturation rounding, the text limits, "at least one measurement", and the derived BMI. Who is acting, and when, are not measurements and stay with the callers.
/// </summary>
internal static class VitalsFieldRules
{
    /// <summary>What the rules resolved from a <see cref="VitalsInput"/>, in the stored (US) units.</summary>
    internal sealed record Resolved(
        decimal? WeightPounds, decimal? HeightInches, decimal? TemperatureFahrenheit, string? TemperatureMethod, int? Systolic, int? Diastolic, int? Pulse,
        int? Respiration, decimal? OxygenSaturation, decimal? WaistInches, decimal? HeadInches, decimal? Bmi, string? BmiStatus, string? Note,
        IReadOnlyList<string> MeasurementsTaken);

    /// <summary>
    /// Checks every measurement and collects every violation into <paramref name="errors"/> (DOM-3); never throws. The result is meaningful only when <paramref name="errors"/> is still
    /// empty after the caller has added its own checks.
    /// </summary>
    public static Resolved Validate(VitalsInput input, List<string> errors)
    {
        var taken = new List<string>();

        var weight = Convert(MeasurementKind.Weight, "Weight", input.Weight, VitalsRecording.MinWeightPounds, VitalsRecording.MaxWeightPounds, "lb", errors, taken, "weight", lowerInclusive: true);
        var height = Convert(MeasurementKind.Height, "Height", input.Height, VitalsRecording.MinHeightInches, VitalsRecording.MaxHeightInches, "in", errors, taken, "height", lowerInclusive: true);
        var temperature = Convert(MeasurementKind.Temperature, "Temperature", input.Temperature,
            VitalsRecording.MinTemperatureFahrenheit, VitalsRecording.MaxTemperatureFahrenheit, "degF", errors, taken, "temperature", lowerInclusive: true);
        var waist = Convert(MeasurementKind.WaistCircumference, "Waist circumference", input.WaistCircumference, 0m,
            VitalsRecording.MaxCircumferenceInches, "in", errors, taken, "waistCircumference");
        var head = Convert(MeasurementKind.HeadCircumference, "Head circumference", input.HeadCircumference, 0m,
            VitalsRecording.MaxCircumferenceInches, "in", errors, taken, "headCircumference");

        CheckWhole(input.Systolic, VitalsRecording.MaxBloodPressure, "Systolic blood pressure", errors, taken, "bloodPressureSystolic");
        CheckWhole(input.Diastolic, VitalsRecording.MaxBloodPressure, "Diastolic blood pressure", errors, taken, "bloodPressureDiastolic");
        CheckWhole(input.Pulse, VitalsRecording.MaxPulse, "Pulse", errors, taken, "pulse");
        CheckWhole(input.Respiration, VitalsRecording.MaxRespiration, "Respiration", errors, taken, "respiration");

        // form_vitals.oxygen_saturation is decimal(6,2); rounding here means the value the caller is told about is the one stored.
        decimal? saturationStored = null;
        if (input.OxygenSaturation is { } saturation)
        {
            if (saturation <= 0m || saturation > VitalsRecording.MaxOxygenSaturation)
            {
                errors.Add($"Oxygen saturation must be greater than 0 and at most {VitalsRecording.MaxOxygenSaturation:0}.");
            }
            else
            {
                saturationStored = Math.Round(saturation, 2, MidpointRounding.AwayFromZero);
                taken.Add("oxygenSaturation");
            }
        }

        if (input.Note is { Length: > VitalsRecording.MaxTextLength })
            errors.Add($"Note must be at most {VitalsRecording.MaxTextLength} characters.");

        if (input.TemperatureMethod is { Length: > VitalsRecording.MaxTextLength })
            errors.Add($"Temperature method must be at most {VitalsRecording.MaxTextLength} characters.");

        // CV-2 (new): an empty form is not a measurement.
        if (taken.Count == 0 && errors.Count == 0)
            errors.Add("At least one measurement is required.");

        // CV-5 and CV-6: BMI is derived from the stored values, never taken from the caller.
        var bmi = errors.Count == 0 ? BmiRule.Calculate(weight, height) : null;
        var status = bmi is { } value && BmiRule.Categorize(value) is { } category ? BmiRule.Label(category) : null;

        // CV-7: legacy clears the method when there is no temperature.
        var method = temperature is null || string.IsNullOrEmpty(input.TemperatureMethod) ? null : input.TemperatureMethod;

        return new Resolved(
            weight, height, temperature, method, input.Systolic, input.Diastolic, input.Pulse, input.Respiration, saturationStored, waist, head, bmi, status,
            string.IsNullOrEmpty(input.Note) ? null : input.Note, taken);
    }

    private static decimal? Convert(
        MeasurementKind kind, string label, Quantity? quantity, decimal lower, decimal upper, string storedUnit,
        List<string> errors, List<string> taken, string name, bool lowerInclusive = false)
    {
        if (quantity is null)
            return null;

        if (!UnitConversion.IsAcceptable(kind, quantity.Unit))
        {
            errors.Add($"{label} must be given in {Accepted(kind)}.");
            return null;
        }

        // Before any arithmetic: a number this large would overflow System.Decimal inside the conversion (review finding R-3).
        // It is out of bounds in every unit, so it is reported with the same message as any other out-of-bounds value.
        if (Math.Abs(quantity.Value) > VitalsRecording.MaxRawMeasurement)
        {
            errors.Add(BoundsMessage(label, lower, upper, storedUnit, lowerInclusive));
            return null;
        }

        var stored = UnitConversion.ToStored(kind, quantity);
        var aboveLower = lowerInclusive ? stored >= lower : stored > lower;
        if (!aboveLower || stored > upper)
        {
            errors.Add(BoundsMessage(label, lower, upper, storedUnit, lowerInclusive));
            return null;
        }

        taken.Add(name);
        return stored;
    }

    private static string BoundsMessage(string label, decimal lower, decimal upper, string unit, bool lowerInclusive) =>
        lowerInclusive
            ? $"{label} must be between {lower:0.##} and {upper:0.##} {unit}."
            : $"{label} must be greater than {lower:0.##} and at most {upper:0.##} {unit}.";

    private static void CheckWhole(int? value, int max, string label, List<string> errors, List<string> taken, string name)
    {
        if (value is null)
            return;

        if (value < 1 || value > max)
            errors.Add($"{label} must be between 1 and {max}.");
        else
            taken.Add(name);
    }

    private static string Accepted(MeasurementKind kind) => kind switch
    {
        MeasurementKind.Weight => "lb or kg",
        MeasurementKind.Temperature => "degF or degC",
        _ => "in or cm"
    };
}
