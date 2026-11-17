using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Domain.Allergies;

/// <summary>The identity a new allergy is given: its uuid, handed in rather than invented inside the domain (DOM-7).</summary>
public sealed record AllergyIdentity
{
    public Guid Uuid { get; }

    private AllergyIdentity(Guid uuid) => Uuid = uuid;

    public static AllergyIdentity Create(Guid uuid) =>
        uuid == Guid.Empty
            ? throw new ClinicalCareValidationException(["The allergy identity must have a uuid."])
            : new AllergyIdentity(uuid);
}
