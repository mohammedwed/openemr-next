using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Domain;

/// <summary>
/// A clinical encounter: one interaction between a patient and a provider, optionally at a facility. The
/// aggregate root of Clinical Care's first slice.
/// </summary>
/// <remarks>
/// Identity (DOM-6): <see cref="Uuid"/> is the public identity; <see cref="Number"/> (<c>form_encounter.encounter</c>)
/// is a correlation key legacy children still join on, protected in the schema by only a non-unique index;
/// <see cref="Id"/> (<c>form_encounter.id</c>) is the row's primary key, assigned by the database on insert and
/// <c>0</c> until then. <see cref="Uuid"/> and <see cref="Number"/> are handed in (DOM-7), never invented here.
/// <para>
/// The patient is held by reference (<see cref="Patient"/>); only <c>pid</c> is a column in
/// <c>form_encounter</c>, so the persisted form carries the pid and the adapter resolves the uuid when reading.
/// </para>
/// <para>
/// Slice 1 has no mutating method: updates stay with the legacy writers until they are inventoried column by
/// column (ADR 0064 #2). Add one narrow method per legitimate change, never a public setter (DOM-5).
/// </para>
/// </remarks>
public sealed class Encounter
{
    public long Id { get; private set; }
    public Guid Uuid { get; }
    public long Number { get; }
    public PatientReference Patient { get; }
    public string ClassCode { get; }
    public int CalendarCategoryId { get; }
    public DateTime Date { get; }
    public DateTime? OnsetDate { get; }
    public string? Reason { get; }
    public int FacilityId { get; }
    public string? FacilityName { get; }
    public int? PosCode { get; }
    public long ProviderId { get; }
    public string? Sensitivity { get; }

    private Encounter(EncounterCreation creation, EncounterIdentity identity)
    {
        Uuid = identity.Uuid;
        Number = identity.Number;
        Patient = creation.Patient;
        ClassCode = creation.ClassCode;
        CalendarCategoryId = creation.CalendarCategoryId;
        Date = creation.Date;
        OnsetDate = creation.OnsetDate;
        Reason = creation.Reason;
        FacilityId = creation.FacilityId;
        FacilityName = creation.FacilityName;
        PosCode = creation.PosCode;
        ProviderId = creation.ProviderId;
        Sensitivity = creation.Sensitivity;
    }

    /// <summary>
    /// Creates an encounter from an already-valid <paramref name="creation"/> (<see cref="EncounterCreation.Create"/>
    /// owns the rules; this trusts its input) and its allocated <paramref name="identity"/>.
    /// </summary>
    public static Encounter Create(EncounterCreation creation, EncounterIdentity identity) =>
        new(creation, identity);

    /// <summary>
    /// Called only by the Infrastructure adapter after the insert, to record the database-assigned primary key
    /// (the same narrow, internal exception <c>InsurancePolicy.AssignGeneratedIdentity</c> is, DOM-5). Internal,
    /// so only <c>Infrastructure</c> and the unit tests can reach it.
    /// </summary>
    internal void AssignGeneratedId(long id)
    {
        if (Id != 0)
            throw new InvalidOperationException("The encounter's database id has already been assigned.");

        Id = id;
    }
}
