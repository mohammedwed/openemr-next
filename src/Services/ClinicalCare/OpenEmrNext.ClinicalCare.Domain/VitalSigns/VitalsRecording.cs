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

        // The measurements, notes and the "at least one" rule are shared with VitalsChange (VitalsFieldRules); who is acting is not.
        var fields = VitalsFieldRules.Validate(input, errors);

        // The provider must be identifiable (as CC-10 for encounters), and its id must fit the int column legacy keeps it in
        // (review finding R-1).
        if (providerId is <= 0 or > EncounterCreation.MaxProviderId)
            errors.Add("ProviderId must identify the acting provider.");

        if (errors.Count > 0)
            throw new VitalsValidationException(errors);

        return new VitalsRecording(
            encounter, providerId, now, fields.WeightPounds, fields.HeightInches, fields.TemperatureFahrenheit, fields.TemperatureMethod,
            fields.Systolic, fields.Diastolic, fields.Pulse, fields.Respiration, fields.OxygenSaturation, fields.WaistInches, fields.HeadInches,
            fields.Bmi, fields.BmiStatus, fields.Note, fields.MeasurementsTaken);
    }
}
