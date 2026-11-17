using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Domain.Problems;

/// <summary>
/// A problem on a patient's list: one entry of legacy's shared <c>lists</c> table whose <c>type</c> is <c>medical_problem</c> (ADR 0070). The
/// aggregate root of Clinical Care's problem slice.
/// </summary>
/// <remarks>
/// Identity (DOM-6): <see cref="Uuid"/> is the public identity; <see cref="Id"/> (<c>lists.id</c>) is the row's primary key, assigned by the database
/// on insert and <c>0</c> until then. The uuid is handed in (DOM-7). The patient is held by reference; only <c>pid</c> is a column.
/// <para>
/// This slice has no mutating method: updating is the next slice, and will add one narrow method per legitimate change, never a public setter (DOM-5).
/// </para>
/// </remarks>
public sealed class Problem
{
    public long Id { get; private set; }
    public Guid Uuid { get; }
    public PatientReference Patient { get; }
    public string Title { get; }
    public DateTime BeginDate { get; }
    public DateTime? EndDate { get; }
    public string? Diagnosis { get; }
    public int Outcome { get; }
    public int Occurrence { get; }
    public string? ReferredBy { get; }
    public string? Comments { get; }
    public long ProviderId { get; }

    /// <summary>When the problem was recorded (<c>lists.date</c>): the clock's now, not the date the problem began (ADR 0070 #5).</summary>
    public DateTime RecordedAt { get; }

    private Problem(ProblemRecording recording, ProblemIdentity identity, DateTime recordedAt)
    {
        Uuid = identity.Uuid;
        Patient = recording.Patient;
        Title = recording.Title;
        BeginDate = recording.BeginDate;
        EndDate = recording.EndDate;
        Diagnosis = recording.Diagnosis;
        Outcome = recording.Outcome;
        Occurrence = recording.Occurrence;
        ReferredBy = recording.ReferredBy;
        Comments = recording.Comments;
        ProviderId = recording.ProviderId;
        RecordedAt = recordedAt;
    }

    /// <summary>Records a problem from an already-valid <paramref name="recording"/> (the factory owns the rules) and its <paramref name="identity"/>.</summary>
    /// <param name="recordedAt">The instant of recording, from the clock (DOM-8).</param>
    public static Problem Record(ProblemRecording recording, ProblemIdentity identity, DateTime recordedAt) =>
        new(recording, identity, recordedAt);

    /// <summary>Called only by the Infrastructure adapter after the insert, to record the database-assigned key (the narrow, internal exception, DOM-5).</summary>
    internal void AssignGeneratedId(long id)
    {
        if (Id != 0)
            throw new InvalidOperationException("The problem's database id has already been assigned.");

        Id = id;
    }
}
