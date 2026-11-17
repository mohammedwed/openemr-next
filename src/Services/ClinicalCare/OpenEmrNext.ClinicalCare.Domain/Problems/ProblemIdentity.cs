using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Domain.Problems;

/// <summary>
/// The identity a new problem is given: its uuid, handed in rather than invented inside the domain (DOM-7). Unlike an encounter there is no
/// human-visible number: legacy joins an issue by its row id and uuid only.
/// </summary>
public sealed record ProblemIdentity
{
    public Guid Uuid { get; }

    private ProblemIdentity(Guid uuid) => Uuid = uuid;

    public static ProblemIdentity Create(Guid uuid) =>
        uuid == Guid.Empty
            ? throw new ClinicalCareValidationException(["The problem identity must have a uuid."])
            : new ProblemIdentity(uuid);
}
