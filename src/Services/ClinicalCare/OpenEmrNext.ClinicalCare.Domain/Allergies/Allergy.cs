using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Domain.Allergies;

/// <summary>
/// An allergy on a patient's list: one entry of legacy's shared <c>lists</c> table whose <c>type</c> is <c>allergy</c> (ADR 0070). The aggregate root of
/// Clinical Care's allergy slice.
/// </summary>
/// <remarks>
/// Identity (DOM-6): <see cref="Uuid"/> is the public identity; <see cref="Id"/> (<c>lists.id</c>) is the row's primary key, assigned by the database on
/// insert and <c>0</c> until then. This slice has no mutating method: updating is a later slice, and will add one narrow method per legitimate change,
/// never a public setter (DOM-5).
/// </remarks>
public sealed class Allergy
{
    public long Id { get; private set; }
    public Guid Uuid { get; }
    public PatientReference Patient { get; }
    public string Title { get; }
    public DateTime? BeginDate { get; }
    public DateTime? EndDate { get; }
    public string? Diagnosis { get; }
    public string? Reaction { get; }
    public string? Severity { get; }
    public string? Verification { get; }
    public string? Comments { get; }
    public long ProviderId { get; }

    /// <summary>When the allergy was recorded (<c>lists.date</c>): the clock's now, not the date it began (ADR 0070 #5).</summary>
    public DateTime RecordedAt { get; }

    private Allergy(AllergyRecording recording, AllergyIdentity identity, DateTime recordedAt)
    {
        Uuid = identity.Uuid;
        Patient = recording.Patient;
        Title = recording.Title;
        BeginDate = recording.BeginDate;
        EndDate = recording.EndDate;
        Diagnosis = recording.Diagnosis;
        Reaction = recording.Reaction;
        Severity = recording.Severity;
        Verification = recording.Verification;
        Comments = recording.Comments;
        ProviderId = recording.ProviderId;
        RecordedAt = recordedAt;
    }

    /// <summary>Records an allergy from an already-valid <paramref name="recording"/> (the factory owns the rules) and its <paramref name="identity"/>.</summary>
    public static Allergy Record(AllergyRecording recording, AllergyIdentity identity, DateTime recordedAt) =>
        new(recording, identity, recordedAt);

    /// <summary>Called only by the Infrastructure adapter after the insert, to record the database-assigned key (the narrow, internal exception, DOM-5).</summary>
    internal void AssignGeneratedId(long id)
    {
        if (Id != 0)
            throw new InvalidOperationException("The allergy's database id has already been assigned.");

        Id = id;
    }
}
