using OpenEmrNext.ClinicalCare.Domain.VitalSigns;

namespace OpenEmrNext.ClinicalCare.Domain.ValueObjects;

/// <summary>
/// How vitals refer to their encounter: by reference, with the three things legacy's own link needs. Legacy joins a
/// vitals row to its encounter through <c>forms</c> using the encounter <b>number</b> and the patient's <b>pid</b>
/// (<c>form_vitals</c> has no encounter column), while the public identity is the <see cref="Uuid"/>.
/// </summary>
public sealed record EncounterReference
{
    public Guid Uuid { get; }
    public long Number { get; }
    public PatientReference Patient { get; }

    private EncounterReference(Guid uuid, long number, PatientReference patient)
    {
        Uuid = uuid;
        Number = number;
        Patient = patient;
    }

    /// <summary>
    /// Vitals can only be attached to an encounter that has a number and a patient. A half-filled legacy row may have
    /// neither; it can be read but not extended (DOM-9: tolerant reads, strict writes).
    /// </summary>
    /// <exception cref="VitalsValidationException">The encounter has no number or no known patient.</exception>
    public static EncounterReference Create(Guid uuid, long? number, Guid? patientUuid, long? patientPid)
    {
        var errors = new List<string>();

        if (number is not > 0)
            errors.Add("The encounter has no encounter number, so vitals cannot be attached to it.");

        if (patientUuid is null || patientUuid == Guid.Empty || patientPid is not > 0)
            errors.Add("The encounter's patient is unknown, so vitals cannot be attached to it.");

        if (errors.Count > 0)
            throw new VitalsValidationException(errors);

        return new EncounterReference(uuid, number!.Value, PatientReference.Create(patientUuid!.Value, patientPid!.Value));
    }

    /// <summary>
    /// The same check as <see cref="Create"/>, for a caller that wants "can this encounter take vitals?" as an answer, not an
    /// exception: reading the vitals of a half-filled legacy encounter is not an error, it simply has none (DOM-9, tolerant reads).
    /// </summary>
    /// <returns>The reference, or <c>null</c> when the encounter has no number or no known patient.</returns>
    public static EncounterReference? TryCreate(Guid uuid, long? number, Guid? patientUuid, long? patientPid)
    {
        try
        {
            return Create(uuid, number, patientUuid, patientPid);
        }
        catch (VitalsValidationException)
        {
            return null;
        }
    }
}
