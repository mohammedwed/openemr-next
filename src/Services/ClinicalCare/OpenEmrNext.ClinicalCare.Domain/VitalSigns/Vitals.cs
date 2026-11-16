using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Domain.VitalSigns;

/// <summary>
/// One set of vital signs recorded during an encounter. Aggregate root of Clinical Care's second slice.
/// </summary>
/// <remarks>
/// Identity (DOM-6): <see cref="Uuid"/> is the public identity, handed in (DOM-7); <see cref="Id"/>
/// (<c>form_vitals.id</c>) is the row's auto-increment key, <c>0</c> until the database assigns it. The encounter is held
/// by reference (<see cref="Encounter"/>): <c>form_vitals</c> has no encounter column, so legacy's link is the
/// <c>forms</c> row the adapter writes. Every measurement is in the stored US unit (ADR 0065). Slice 2 has no mutating
/// method: update and delete wait for a concurrency token and audit (KD-8).
/// </remarks>
public sealed class Vitals
{
    public long Id { get; private set; }
    public Guid Uuid { get; }
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
    public IReadOnlyList<string> MeasurementsTaken { get; }

    private Vitals(VitalsRecording recording, VitalsIdentity identity)
    {
        Uuid = identity.Uuid;
        Encounter = recording.Encounter;
        ProviderId = recording.ProviderId;
        RecordedAt = recording.RecordedAt;
        WeightPounds = recording.WeightPounds;
        HeightInches = recording.HeightInches;
        TemperatureFahrenheit = recording.TemperatureFahrenheit;
        TemperatureMethod = recording.TemperatureMethod;
        Systolic = recording.Systolic;
        Diastolic = recording.Diastolic;
        Pulse = recording.Pulse;
        Respiration = recording.Respiration;
        OxygenSaturation = recording.OxygenSaturation;
        WaistInches = recording.WaistInches;
        HeadInches = recording.HeadInches;
        Bmi = recording.Bmi;
        BmiStatus = recording.BmiStatus;
        Note = recording.Note;
        MeasurementsTaken = recording.MeasurementsTaken;
    }

    /// <summary>Records vitals from an already-valid <paramref name="recording"/> (it owns the rules) and its allocated identity.</summary>
    public static Vitals Record(VitalsRecording recording, VitalsIdentity identity) => new(recording, identity);

    /// <summary>
    /// Called only by the Infrastructure adapter after the insert, to record the database-assigned primary key (the same
    /// narrow internal exception <c>Encounter.AssignGeneratedId</c> is, DOM-5).
    /// </summary>
    internal void AssignGeneratedId(long id)
    {
        if (Id != 0)
            throw new InvalidOperationException("The vitals' database id has already been assigned.");

        Id = id;
    }
}
