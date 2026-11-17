namespace OpenEmrNext.ClinicalCare.Domain.ReadModels;

/// <summary>
/// An allergy as a caller reads it: a query result, not an aggregate (ADR 0054). Built tolerantly from whatever a legacy row holds (DOM-9, LEG-4), because
/// the issue screen, REST and imports all write <c>lists</c> and may leave columns empty that this service's own path always fills.
/// </summary>
/// <param name="Uuid">The public identity. A row without one is not addressable and never becomes a view (ADR 0070 #6).</param>
/// <param name="PatientUuid">The patient's uuid, or <c>null</c> when the row's <c>pid</c> matches no known patient.</param>
/// <param name="PatientPid">The legacy correlation key.</param>
/// <param name="Title">The substance; <c>null</c> if the legacy row has none.</param>
/// <param name="BeginDate">When the allergy began, if recorded.</param>
/// <param name="EndDate">When it ended; <c>null</c> while active.</param>
/// <param name="Diagnosis">The coded diagnosis text, as stored.</param>
/// <param name="Reaction">The <c>reaction</c> list code, or <c>null</c> when none (legacy stores an empty string).</param>
/// <param name="Severity">The <c>severity_ccda</c> list code, or <c>null</c> when none.</param>
/// <param name="Verification">The <c>allergyintolerance-verification</c> code, or <c>null</c> when none.</param>
/// <param name="Comments">Free-text comments.</param>
/// <param name="RecordedAt">When the row was created (<c>lists.date</c>).</param>
/// <param name="RecordedBy">The username legacy recorded as having made it, if any.</param>
/// <param name="LastModified">When the row last changed, by any writer: the basis of the concurrency token (ADR 0068, ADR 0070 #4).</param>
public sealed record AllergyView(
    Guid Uuid,
    Guid? PatientUuid,
    long? PatientPid,
    string? Title,
    DateTime? BeginDate,
    DateTime? EndDate,
    string? Diagnosis,
    string? Reaction,
    string? Severity,
    string? Verification,
    string? Comments,
    DateTime? RecordedAt,
    string? RecordedBy,
    DateTime? LastModified);

/// <summary>One page of a patient's allergies, newest first, with the total so a caller can page.</summary>
public sealed record AllergyPage(IReadOnlyList<AllergyView> Items, int Total);
