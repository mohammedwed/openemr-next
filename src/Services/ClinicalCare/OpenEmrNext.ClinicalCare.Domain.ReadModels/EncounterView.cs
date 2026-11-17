namespace OpenEmrNext.ClinicalCare.Domain.ReadModels;

/// <summary>
/// An encounter as a caller reads it: a query result, not an aggregate (ADR 0054). Built tolerantly from whatever
/// a legacy row holds (DOM-9, LEG-4), because other legacy writers created most rows and may have left columns
/// empty that this service's own creation path always fills. Hence the nullable members: a half-filled legacy
/// row is still shown, not turned into an error that hides every other encounter of the patient.
/// </summary>
/// <param name="Uuid">The public identity.</param>
/// <param name="Number">Legacy's encounter number (<c>form_encounter.encounter</c>), if the row has one.</param>
/// <param name="PatientUuid">The patient's uuid, or <c>null</c> when the row's <c>pid</c> matches no patient.</param>
/// <param name="PatientPid">The legacy correlation key, if the row has one.</param>
/// <param name="ClassCode">The encounter class code; empty if the legacy row has none.</param>
/// <param name="Date">The encounter date, if the row has one.</param>
/// <param name="OnsetDate">The onset date, if any.</param>
/// <param name="Reason">The reason for the visit, if any.</param>
/// <param name="FacilityId">The facility id; <c>0</c> for none.</param>
/// <param name="FacilityName">The facility's name as legacy copied it onto the row.</param>
/// <param name="PosCode">The place-of-service code, if any.</param>
/// <param name="ProviderId">The provider id; <c>0</c> when legacy recorded none.</param>
/// <param name="Sensitivity">The legacy sensitivity label. Returned, not enforced (finding F-CC-2).</param>
/// <param name="CalendarCategoryId">Legacy's <c>pc_catid</c>, if the row has one: what kind of visit the calendar shows it as.</param>
/// <param name="LastUpdate">When the row last changed, by any writer: the basis of the concurrency token (ADR 0068). <c>null</c> only for a row the database did not stamp.</param>
public sealed record EncounterView(
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
    DateTime? LastUpdate = null,
    int? CalendarCategoryId = null);

/// <summary>One page of a patient's encounters, newest first, with the total so a caller can page.</summary>
/// <param name="Items">The encounters on this page.</param>
/// <param name="Total">How many encounters the patient has in all (soft-deleted ones excluded, CC-9).</param>
public sealed record EncounterPage(IReadOnlyList<EncounterView> Items, int Total);
