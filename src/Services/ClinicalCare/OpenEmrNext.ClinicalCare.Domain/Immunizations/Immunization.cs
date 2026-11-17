using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Domain.Immunizations;

/// <summary>The identity a new immunization is given: its uuid, handed in rather than invented inside the domain (DOM-7).</summary>
public sealed record ImmunizationIdentity
{
    public Guid Uuid { get; }

    private ImmunizationIdentity(Guid uuid) => Uuid = uuid;

    public static ImmunizationIdentity Create(Guid uuid) =>
        uuid == Guid.Empty
            ? throw new ClinicalCareValidationException(["The immunization identity must have a uuid."])
            : new ImmunizationIdentity(uuid);
}

/// <summary>
/// An immunization on a patient's record: one row of legacy's <c>immunizations</c> table (M5d slice 2, ADR 0072). The aggregate root of Clinical Care's immunization slice.
/// </summary>
/// <remarks>
/// Identity (DOM-6): <see cref="Uuid"/> is the public identity; <see cref="Id"/> (<c>immunizations.id</c>) is the row's key, assigned by the database on insert and <c>0</c> until then. The patient is held by reference;
/// only the pid is a column. Its clinical content is <see cref="Facts"/>, validated once for every way an immunization is made or changed. This slice has no mutating method on the aggregate: a correction replaces the
/// stored facts through its own use case (DOM-5).
/// </remarks>
public sealed class Immunization
{
    public long Id { get; private set; }
    public Guid Uuid { get; }
    public PatientReference Patient { get; }
    public ImmunizationFacts Facts { get; }
    public long ProviderId { get; }

    /// <summary>When it was recorded (<c>create_date</c>): the clock's now, not the date it was administered.</summary>
    public DateTime RecordedAt { get; }

    private Immunization(PatientReference patient, ImmunizationFacts facts, long providerId, ImmunizationIdentity identity, DateTime recordedAt)
    {
        Uuid = identity.Uuid;
        Patient = patient;
        Facts = facts;
        ProviderId = providerId;
        RecordedAt = recordedAt;
    }

    /// <summary>The provider id is stored in <c>created_by</c> and <c>updated_by</c> (users.id, a bigint); the same bound as every provider id in this service (review finding R-1).</summary>
    public const long MaxProviderId = int.MaxValue;

    /// <summary>Records an immunization from already-valid <paramref name="facts"/>, for <paramref name="patient"/>, by the acting provider (caller-supplied until identity forwarding exists, F-CC-1).</summary>
    /// <exception cref="ClinicalCareValidationException">The provider id is not a user id this column can hold.</exception>
    public static Immunization Record(PatientReference patient, ImmunizationFacts facts, long providerId, ImmunizationIdentity identity, DateTime recordedAt)
    {
        ArgumentNullException.ThrowIfNull(patient);
        ArgumentNullException.ThrowIfNull(facts);

        if (providerId is <= 0 or > MaxProviderId)
            throw new ClinicalCareValidationException(["ProviderId must identify the acting provider."]);

        return new Immunization(patient, facts, providerId, identity, recordedAt);
    }

    /// <summary>Called only by the Infrastructure adapter after the insert, to record the database-assigned key (the narrow, internal exception, DOM-5).</summary>
    internal void AssignGeneratedId(long id)
    {
        if (Id != 0)
            throw new InvalidOperationException("The immunization's database id has already been assigned.");

        Id = id;
    }
}
