namespace OpenEmrNext.ClinicalCare.Domain.VitalSigns;

/// <summary>
/// The identity of a new vitals record, handed to the aggregate rather than invented by it (DOM-7, DOM-8). Vitals have
/// no legacy business number: <c>form_vitals.id</c> is auto-increment and is the database's to assign.
/// </summary>
public sealed record VitalsIdentity
{
    public Guid Uuid { get; }

    private VitalsIdentity(Guid uuid) => Uuid = uuid;

    /// <exception cref="VitalsValidationException">The uuid is empty.</exception>
    public static VitalsIdentity Create(Guid uuid) =>
        uuid == Guid.Empty ? throw new VitalsValidationException(["The vitals identity must have a uuid."]) : new VitalsIdentity(uuid);
}
