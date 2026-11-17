using OpenEmrNext.ClinicalCare.Domain.ReadModels;

namespace OpenEmrNext.ClinicalCare.Api.Contracts.Responses;

/// <summary>What <c>POST /api/v1/problems</c> returns: the new problem's identity and the patient it is on.</summary>
/// <param name="Uuid">The problem's public identity.</param>
/// <param name="PatientUuid">The patient the problem was recorded for.</param>
public sealed record ProblemRecordedResponse(Guid Uuid, Guid PatientUuid);

/// <summary>
/// One problem as the API returns it. A contract type of its own, not the read model, so the wire shape and its names are pinned here (ARC-7, API-2).
/// </summary>
/// <param name="Uuid">The problem's public identity.</param>
/// <param name="PatientUuid">The patient's uuid, or <c>null</c> when the row's pid matches no known patient.</param>
/// <param name="PatientPid">Legacy's patient key, for compatibility with legacy tooling only.</param>
/// <param name="Title">The problem's name.</param>
/// <param name="BeginDate">When the problem began, in the server's local time like legacy.</param>
/// <param name="EndDate">When it ended; <c>null</c> while it is active. This, not a flag, is how legacy shows a problem as resolved.</param>
/// <param name="Diagnosis">The coded diagnosis, <c>SYSTEM:code</c> pairs separated by semicolons.</param>
/// <param name="Outcome">The <c>outcome</c> list code; <c>0</c> is unassigned.</param>
/// <param name="Occurrence">The <c>occurrence</c> list code; <c>0</c> is unknown.</param>
/// <param name="ReferredBy">Who referred the patient, if recorded.</param>
/// <param name="Comments">Free-text comments.</param>
/// <param name="RecordedAt">When the problem was recorded (not when it began).</param>
/// <param name="RecordedBy">The username legacy recorded as having recorded it, if any.</param>
/// <param name="Verification">The <c>condition-verification</c> code, if recorded.</param>
/// <param name="ETag">The record's current version as a strong entity tag, the same value the <c>ETag</c> header carries. Send it back in <c>If-Match</c> to update the problem. A list response has no per-item header, which is why it is also here.</param>
public sealed record ProblemResponse(
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
    string? Verification = null,
    string? ETag = null)
{
    public static ProblemResponse From(ProblemView view) => new(
        view.Uuid, view.PatientUuid, view.PatientPid, view.Title, view.BeginDate, view.EndDate, view.Diagnosis, view.Outcome, view.Occurrence,
        view.ReferredBy, view.Comments, view.RecordedAt, view.RecordedBy, view.Verification,
        OpenEmrNext.ClinicalCare.Api.Endpoints.Preconditions.ETagOf(view.LastModified));
}

/// <summary>One bounded page of a patient's problems, newest first, with the total so a caller can page.</summary>
public sealed record ProblemListResponse(IReadOnlyList<ProblemResponse> Items, int Total, int Limit, int Offset);
