namespace OpenEmrNext.ClinicalCare.Domain.ValueObjects;

/// <summary>
/// The two identifiers a new encounter needs, handed to the aggregate rather than invented by it (DOM-7,
/// DOM-8): the public <see cref="Uuid"/> (the identity other services and APIs use, DOM-6) and the legacy
/// <see cref="Number"/> (<c>form_encounter.encounter</c>), allocated from the same shared <c>sequences</c>
/// counter every legacy writer uses (CC-5, ADR 0064). The number is a correlation key, not the identity:
/// the schema protects it with only a non-unique index.
/// </summary>
public sealed record EncounterIdentity
{
    public Guid Uuid { get; }
    public long Number { get; }

    private EncounterIdentity(Guid uuid, long number)
    {
        Uuid = uuid;
        Number = number;
    }

    /// <exception cref="ClinicalCareValidationException">The uuid is empty or the number is not positive.</exception>
    public static EncounterIdentity Create(Guid uuid, long number)
    {
        var errors = new List<string>();

        if (uuid == Guid.Empty)
            errors.Add("The encounter identity must have a uuid.");

        if (number <= 0)
            errors.Add("The encounter identity must have a positive encounter number.");

        if (errors.Count > 0)
            throw new ClinicalCareValidationException(errors);

        return new EncounterIdentity(uuid, number);
    }
}
