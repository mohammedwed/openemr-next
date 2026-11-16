namespace OpenEmrNext.ClinicalCare.Domain.ValueObjects;

/// <summary>
/// How Clinical Care refers to a patient: by reference only, never a copy of their demographics (design set
/// R7, decision D1). <see cref="Uuid"/> is the identity; <see cref="Pid"/> is carried as a correlation key
/// only because <c>form_encounter.pid</c> is what legacy tables still join on, and is dropped from public
/// contracts when the shared database ends.
/// </summary>
public sealed record PatientReference
{
    public Guid Uuid { get; }
    public long Pid { get; }

    private PatientReference(Guid uuid, long pid)
    {
        Uuid = uuid;
        Pid = pid;
    }

    /// <exception cref="EncounterValidationException">The uuid is empty or the pid is not positive.</exception>
    public static PatientReference Create(Guid uuid, long pid)
    {
        var errors = new List<string>();

        if (uuid == Guid.Empty)
            errors.Add("The patient reference must have a uuid.");

        if (pid <= 0)
            errors.Add("The patient reference must have a positive pid.");

        if (errors.Count > 0)
            throw new EncounterValidationException(errors);

        return new PatientReference(uuid, pid);
    }
}
