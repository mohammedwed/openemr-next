using OpenEmrNext.ClinicalCare.Domain.ReadModels;

namespace OpenEmrNext.ClinicalCare.Api.Contracts.Responses;

/// <summary>
/// One encounter as the API returns it. A contract type of its own, not the read model, so the wire shape and its
/// names are pinned here (ARC-7, API-2) and cannot drift when the read model changes.
/// </summary>
/// <param name="Uuid">The encounter's public identity.</param>
/// <param name="Number">Legacy's encounter number, if the row has one. A correlation key, not the identity.</param>
/// <param name="PatientUuid">The patient's uuid, or <c>null</c> when the row's pid matches no known patient.</param>
/// <param name="PatientPid">Legacy's patient key, for compatibility with legacy tooling only.</param>
/// <param name="ClassCode">The encounter class code, for example <c>AMB</c>.</param>
/// <param name="Date">The encounter date and time, in the server's local time like legacy.</param>
/// <param name="OnsetDate">The onset date, if any.</param>
/// <param name="Reason">The reason for the visit, if any.</param>
/// <param name="FacilityId">The facility id; <c>0</c> for none.</param>
/// <param name="FacilityName">The facility's name as recorded on the encounter.</param>
/// <param name="PosCode">The place-of-service code, if any.</param>
/// <param name="ProviderId">The provider id; <c>0</c> when none was recorded.</param>
/// <param name="Sensitivity">The legacy sensitivity label. Returned but not yet enforced (finding F-CC-2).</param>
/// <param name="CalendarCategoryId">Legacy's calendar category id (<c>pc_catid</c>), if the row has one.</param>
/// <param name="ETag">The record's current version as a strong entity tag, the same value the <c>ETag</c> header carries. Send it back in <c>If-Match</c> to update the encounter. A list response has no per-item header, which is why it is also here.</param>
public sealed record EncounterResponse(
    Guid Uuid,
    long? Number,
    Guid? PatientUuid,
    long? PatientPid,
    string ClassCode,
    DateTime? Date,
    DateTime? OnsetDate,
    string? Reason,
    int FacilityId,
    string? FacilityName,
    int? PosCode,
    long ProviderId,
    string? Sensitivity,
    int? CalendarCategoryId = null,
    string? ETag = null)
{
    public static EncounterResponse From(EncounterView view) => new(
        view.Uuid, view.Number, view.PatientUuid, view.PatientPid, view.ClassCode, view.Date, view.OnsetDate,
        view.Reason, view.FacilityId, view.FacilityName, view.PosCode, view.ProviderId, view.Sensitivity,
        view.CalendarCategoryId, OpenEmrNext.ClinicalCare.Api.Endpoints.Preconditions.ETagOf(view.LastUpdate));
}
