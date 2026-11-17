using OpenEmrNext.ClinicalCare.Domain.ReadModels;

namespace OpenEmrNext.ClinicalCare.Api.Contracts.Responses;

/// <summary>What <c>POST /api/v1/allergies</c> returns: the new allergy's identity and the patient it is on.</summary>
/// <param name="Uuid">The allergy's public identity.</param>
/// <param name="PatientUuid">The patient the allergy was recorded for.</param>
public sealed record AllergyRecordedResponse(Guid Uuid, Guid PatientUuid);

/// <summary>
/// One allergy as the API returns it. A contract type of its own, not the read model, so the wire shape and its names are pinned here (ARC-7, API-2).
/// </summary>
/// <param name="Uuid">The allergy's public identity.</param>
/// <param name="PatientUuid">The patient's uuid, or <c>null</c> when the row's pid matches no known patient.</param>
/// <param name="PatientPid">Legacy's patient key, for compatibility with legacy tooling only.</param>
/// <param name="Title">The substance.</param>
/// <param name="BeginDate">When the allergy began, if recorded, in the server's local time like legacy.</param>
/// <param name="EndDate">When it ended; <c>null</c> while it is active.</param>
/// <param name="Diagnosis">The coded diagnosis, <c>SYSTEM:code</c> pairs separated by semicolons.</param>
/// <param name="Reaction">The <c>reaction</c> list code, if recorded.</param>
/// <param name="Severity">The <c>severity_ccda</c> list code, if recorded.</param>
/// <param name="Verification">The <c>allergyintolerance-verification</c> code, if recorded.</param>
/// <param name="Comments">Free-text comments.</param>
/// <param name="RecordedAt">When the allergy was recorded (not when it began).</param>
/// <param name="RecordedBy">The username legacy recorded as having recorded it, if any.</param>
/// <param name="ETag">The record's current version as a strong entity tag, the same value the <c>ETag</c> header carries. Send it back in <c>If-Match</c> to update the allergy. A list response has no per-item header, which is why it is also here.</param>
public sealed record AllergyResponse(
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
    string? ETag = null)
{
    public static AllergyResponse From(AllergyView view) => new(
        view.Uuid, view.PatientUuid, view.PatientPid, view.Title, view.BeginDate, view.EndDate, view.Diagnosis, view.Reaction, view.Severity,
        view.Verification, view.Comments, view.RecordedAt, view.RecordedBy,
        OpenEmrNext.ClinicalCare.Api.Endpoints.Preconditions.ETagOf(view.LastModified));
}

/// <summary>One bounded page of a patient's allergies, newest first, with the total so a caller can page.</summary>
public sealed record AllergyListResponse(IReadOnlyList<AllergyResponse> Items, int Total, int Limit, int Offset);
