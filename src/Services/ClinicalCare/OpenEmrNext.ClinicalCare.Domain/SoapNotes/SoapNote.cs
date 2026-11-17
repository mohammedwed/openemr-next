using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Domain.SoapNotes;

/// <summary>
/// A SOAP note on an encounter: one row of legacy's <c>form_soap</c> plus the <c>forms</c> row that links it to its encounter (M5d slice 3, ADR 0073). The aggregate root of Clinical Care's SOAP slice.
/// </summary>
/// <remarks>
/// Identity (DOM-6): a SOAP note has <b>no uuid</b>; <see cref="Id"/> (<c>form_soap.id</c>) is its public identity within its encounter, assigned by the database on insert and <c>0</c> until then. The encounter is
/// held by reference. A correction replaces the stored sections through its own use case; the aggregate has no mutating method (DOM-5).
/// </remarks>
public sealed class SoapNote
{
    public long Id { get; private set; }
    public EncounterReference Encounter { get; }
    public SoapSections Sections { get; }
    public long ProviderId { get; }

    /// <summary>When it was written (<c>form_soap.date</c>): the clock's now. A correction does not change it.</summary>
    public DateTime RecordedAt { get; }

    private SoapNote(EncounterReference encounter, SoapSections sections, long providerId, DateTime recordedAt)
    {
        Encounter = encounter;
        Sections = sections;
        ProviderId = providerId;
        RecordedAt = recordedAt;
    }

    /// <summary>The provider id is looked up for a username and group; the same bound as every provider id in this service.</summary>
    public const long MaxProviderId = int.MaxValue;

    /// <exception cref="ClinicalCareValidationException">The provider id is not a user id this service can use.</exception>
    public static SoapNote Record(EncounterReference encounter, SoapSections sections, long providerId, DateTime recordedAt)
    {
        ArgumentNullException.ThrowIfNull(encounter);
        ArgumentNullException.ThrowIfNull(sections);

        if (providerId is <= 0 or > MaxProviderId)
            throw new ClinicalCareValidationException(["ProviderId must identify the acting provider."]);

        return new SoapNote(encounter, sections, providerId, recordedAt);
    }

    /// <summary>Called only by the Infrastructure adapter after the insert, to record the database-assigned key (the narrow, internal exception, DOM-5).</summary>
    internal void AssignGeneratedId(long id)
    {
        if (Id != 0)
            throw new InvalidOperationException("The SOAP note's database id has already been assigned.");

        Id = id;
    }
}
