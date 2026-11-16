using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Domain.VitalSigns;

/// <summary>
/// The validated facts needed to record vitals, in the **stored** (US) units (ADR 0065). <see cref="Create"/> is the
/// only way to get one, so <see cref="Vitals.Record"/> never re-checks what is already guaranteed (DOM-2), and every
/// violation is collected before failing (DOM-3).
/// </summary>
public sealed record VitalsRecording
{
    // Plausibility bounds (CV-3, all (new)): wide on purpose. They catch a typo or a wrong unit, not a clinical judgement.
    public const decimal MinWeightPounds = 0.1m;
    public const decimal MaxWeightPounds = 1500m;

    /// <summary>
    /// A height floor, not only a ceiling (review finding R-2): BMI is weight over height squared, so a height near zero makes a
    /// BMI far larger than <c>form_vitals.BMI</c> (<c>decimal(12,6)</c>) can hold, a 500. With these floors the largest possible
    /// BMI is 1500 / 5^2 * 703, about 42,000.
    /// </summary>
    public const decimal MinHeightInches = 5m;
    public const decimal MaxHeightInches = 120m;

    /// <summary>
    /// The largest number accepted before any conversion (review finding R-3). <c>System.Decimal</c> overflows inside a
    /// multiplication long before any plausibility bound is reached (<c>7e28 kg * 2.2</c>), which was a 500; anything above this
    /// is rejected as out of bounds first. Far above every legitimate raw value (1500 lb is under 700 kg and about 3,300 cm-equivalents).
    /// </summary>
    public const decimal MaxRawMeasurement = 1_000_000m;
    public const decimal MinTemperatureFahrenheit = 50m;
    public const decimal MaxTemperatureFahrenheit = 120m;
    public const decimal MaxCircumferenceInches = 200m;
    public const int MaxBloodPressure = 400;
    public const int MaxPulse = 400;
    public const int MaxRespiration = 150;
    public const decimal MaxOxygenSaturation = 100m;

    /// <summary><c>form_vitals.note</c>, <c>temp_method</c> and <c>BMI_status</c> are <c>varchar(255)</c>.</summary>
    public const int MaxTextLength = 255;

    public EncounterReference Encounter { get; }
    public long ProviderId { get; }
    public DateTime RecordedAt { get; }
    public decimal? WeightPounds { get; }
    public decimal? HeightInches { get; }
    public decimal? TemperatureFahrenheit { get; }
    public string? TemperatureMethod { get; }
    public int? Systolic { get; }
    public int? Diastolic { get; }
    public int? Pulse { get; }
    public int? Respiration { get; }
    public decimal? OxygenSaturation { get; }
    public decimal? WaistInches { get; }
    public decimal? HeadInches { get; }
    public decimal? Bmi { get; }
    public string? BmiStatus { get; }
    public string? Note { get; }

    /// <summary>The names of the measurements taken, for the event (CV-13): which, never their values.</summary>
    public IReadOnlyList<string> MeasurementsTaken { get; }

    private VitalsRecording(
        EncounterReference encounter, long providerId, DateTime recordedAt, decimal? weight, decimal? height,
        decimal? temperature, string? temperatureMethod, int? systolic, int? diastolic, int? pulse, int? respiration,
        decimal? oxygenSaturation, decimal? waist, decimal? head, decimal? bmi, string? bmiStatus, string? note,
        IReadOnlyList<string> measurementsTaken)
    {
        Encounter = encounter;
        ProviderId = providerId;
        RecordedAt = recordedAt;
        WeightPounds = weight;
        HeightInches = height;
        TemperatureFahrenheit = temperature;
        TemperatureMethod = temperatureMethod;
        Systolic = systolic;
        Diastolic = diastolic;
        Pulse = pulse;
        Respiration = respiration;
        OxygenSaturation = oxygenSaturation;
        WaistInches = waist;
        HeadInches = head;
        Bmi = bmi;
        BmiStatus = bmiStatus;
        Note = note;
        MeasurementsTaken = measurementsTaken;
    }

    /// <param name="encounter">The encounter the vitals belong to (its existence is the use case's check, CV-1).</param>
    /// <param name="input">The raw facts.</param>
    /// <param name="providerId">The acting provider; caller-supplied until identity forwarding exists (CV-9, F-CC-1).</param>
    /// <param name="now">The current instant from the clock (DOM-8).</param>
    /// <exception cref="VitalsValidationException">One or more fields fail validation.</exception>
    public static VitalsRecording Create(EncounterReference encounter, VitalsInput input, long providerId, DateTime now)
    {
        var errors = new List<string>();
        var taken = new List<string>();

        var weight = Convert(MeasurementKind.Weight, "Weight", input.Weight, MinWeightPounds, MaxWeightPounds, "lb", errors, taken, "weight", lowerInclusive: true);
        var height = Convert(MeasurementKind.Height, "Height", input.Height, MinHeightInches, MaxHeightInches, "in", errors, taken, "height", lowerInclusive: true);
        var temperature = Convert(MeasurementKind.Temperature, "Temperature", input.Temperature,
            MinTemperatureFahrenheit, MaxTemperatureFahrenheit, "degF", errors, taken, "temperature", lowerInclusive: true);
        var waist = Convert(MeasurementKind.WaistCircumference, "Waist circumference", input.WaistCircumference, 0m,
            MaxCircumferenceInches, "in", errors, taken, "waistCircumference");
        var head = Convert(MeasurementKind.HeadCircumference, "Head circumference", input.HeadCircumference, 0m,
            MaxCircumferenceInches, "in", errors, taken, "headCircumference");

        CheckWhole(input.Systolic, MaxBloodPressure, "Systolic blood pressure", errors, taken, "bloodPressureSystolic");
        CheckWhole(input.Diastolic, MaxBloodPressure, "Diastolic blood pressure", errors, taken, "bloodPressureDiastolic");
        CheckWhole(input.Pulse, MaxPulse, "Pulse", errors, taken, "pulse");
        CheckWhole(input.Respiration, MaxRespiration, "Respiration", errors, taken, "respiration");

        // form_vitals.oxygen_saturation is decimal(6,2); rounding here means the value the caller is told about is the one stored.
        decimal? saturationStored = null;
        if (input.OxygenSaturation is { } saturation)
        {
            if (saturation <= 0m || saturation > MaxOxygenSaturation)
            {
                errors.Add($"Oxygen saturation must be greater than 0 and at most {MaxOxygenSaturation:0}.");
            }
            else
            {
                saturationStored = Math.Round(saturation, 2, MidpointRounding.AwayFromZero);
                taken.Add("oxygenSaturation");
            }
        }

        if (input.Note is { Length: > MaxTextLength })
            errors.Add($"Note must be at most {MaxTextLength} characters.");

        if (input.TemperatureMethod is { Length: > MaxTextLength })
            errors.Add($"Temperature method must be at most {MaxTextLength} characters.");

        // CV-2 (new): an empty form is not a measurement.
        if (taken.Count == 0 && errors.Count == 0)
            errors.Add("At least one measurement is required.");

        // The provider must be identifiable (as CC-10 for encounters), and its id must fit the int column legacy keeps it in
        // (review finding R-1).
        if (providerId is <= 0 or > EncounterCreation.MaxProviderId)
            errors.Add("ProviderId must identify the acting provider.");

        if (errors.Count > 0)
            throw new VitalsValidationException(errors);

        // CV-5 and CV-6: BMI is derived from the stored values, never taken from the caller.
        var bmi = BmiRule.Calculate(weight, height);
        var status = bmi is { } value && BmiRule.Categorize(value) is { } category ? BmiRule.Label(category) : null;

        // CV-7: legacy clears the method when there is no temperature.
        var method = temperature is null || string.IsNullOrEmpty(input.TemperatureMethod) ? null : input.TemperatureMethod;

        return new VitalsRecording(
            encounter, providerId, now, weight, height, temperature, method, input.Systolic, input.Diastolic,
            input.Pulse, input.Respiration, saturationStored, waist, head, bmi, status,
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
        if (Math.Abs(quantity.Value) > MaxRawMeasurement)
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
