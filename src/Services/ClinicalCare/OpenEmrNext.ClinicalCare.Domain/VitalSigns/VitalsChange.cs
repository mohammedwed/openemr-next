namespace OpenEmrNext.ClinicalCare.Domain.VitalSigns;

/// <summary>
/// The validated facts of a correction to recorded vitals (M5d slice 1, ADR 0068): the whole measurement set, in the <b>stored</b> (US) units, replacing what is stored.
/// <see cref="Create"/> is the only way to get one, and it applies exactly the measurement rules <see cref="VitalsRecording"/> does (<see cref="VitalsFieldRules"/>), so a
/// correction can never accept a value a recording would refuse.
/// </summary>
/// <remarks>
/// A measurement left out is a measurement <b>not recorded</b>, so correcting a set can remove a reading (it is stored as 0 and reads back as absent, CV-12) and must still leave at
/// least one. The BMI and its category are derived from the corrected weight and height, never taken from the caller (CV-5, CV-6), so they are not fields of the request and not
/// listed as a change of their own.
/// <para>
/// Not editable, by design: who recorded it and when (<c>user</c>, <c>groupname</c>, <c>date</c>), the encounter it belongs to, its uuid, and <c>authorized</c>. The actor of a correction is
/// not a field (ADR 0069 #5).
/// </para>
/// </remarks>
public sealed record VitalsChange
{
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

    private VitalsChange(VitalsFieldRules.Resolved f)
    {
        WeightPounds = f.WeightPounds;
        HeightInches = f.HeightInches;
        TemperatureFahrenheit = f.TemperatureFahrenheit;
        TemperatureMethod = f.TemperatureMethod;
        Systolic = f.Systolic;
        Diastolic = f.Diastolic;
        Pulse = f.Pulse;
        Respiration = f.Respiration;
        OxygenSaturation = f.OxygenSaturation;
        WaistInches = f.WaistInches;
        HeadInches = f.HeadInches;
        Bmi = f.Bmi;
        BmiStatus = f.BmiStatus;
        Note = f.Note;
    }

    /// <exception cref="VitalsValidationException">A measurement is out of bounds, in an unknown unit or missing altogether.</exception>
    public static VitalsChange Create(VitalsInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var errors = new List<string>();
        var fields = VitalsFieldRules.Validate(input, errors);

        if (errors.Count > 0)
            throw new VitalsValidationException(errors);

        return new VitalsChange(fields);
    }
}
