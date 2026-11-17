namespace OpenEmrNext.ClinicalCare.Domain.ReadModels;

/// <summary>
/// A problem as a caller reads it: a query result, not an aggregate (ADR 0054). Built tolerantly from whatever a legacy row holds (DOM-9, LEG-4),
/// because the issue screen, REST and imports all write <c>lists</c> and may leave columns empty that this service's own path always fills.
/// </summary>
/// <param name="Uuid">The public identity. A row without one is not addressable and never becomes a view (ADR 0070 #6).</param>
/// <param name="PatientUuid">The patient's uuid, or <c>null</c> when the row's <c>pid</c> matches no known patient.</param>
/// <param name="PatientPid">The legacy correlation key.</param>
/// <param name="Title">The problem's name; <c>null</c> if the legacy row has none.</param>
/// <param name="BeginDate">When it began, if the row has a date.</param>
/// <param name="EndDate">When it ended; <c>null</c> while active (the issue screen shows a problem as resolved by this, not by <c>activity</c>).</param>
/// <param name="Diagnosis">The coded diagnosis text, <c>SYSTEM:code;SYSTEM:code</c>, as stored.</param>
/// <param name="Outcome">The <c>outcome</c> list code; <c>0</c> is unassigned.</param>
/// <param name="Occurrence">The <c>occurrence</c> list code; <c>0</c> is unknown.</param>
/// <param name="ReferredBy">Who referred the patient, if recorded.</param>
/// <param name="Comments">Free-text comments.</param>
/// <param name="RecordedAt">When the row was created (<c>lists.date</c>).</param>
/// <param name="RecordedBy">The username legacy recorded as having made it, if any.</param>
/// <param name="LastModified">When the row last changed, by any writer: the basis of the concurrency token (ADR 0068, ADR 0070 #4).</param>
public sealed record ProblemView(
    Guid Uuid,
    Guid? PatientUuid,
    long? PatientPid,
    string? Title,
    DateTime? BeginDate,
    DateTime? EndDate,
    string? Diagnosis,
    int Outcome,
    int Occurrence,
    string? ReferredBy,
    string? Comments,
    DateTime? RecordedAt,
    string? RecordedBy,
    DateTime? LastModified);

/// <summary>One page of a patient's problems, newest first, with the total so a caller can page.</summary>
/// <param name="Items">The problems on this page.</param>
/// <param name="Total">How many problems the patient has in all.</param>
public sealed record ProblemPage(IReadOnlyList<ProblemView> Items, int Total);
