namespace OpenEmrNext.ClinicalCare.Api.Contracts.Responses;

/// <summary>One page of a patient's encounters, newest first (API-6).</summary>
/// <param name="Items">The encounters on this page.</param>
/// <param name="Total">How many encounters the patient has in all.</param>
/// <param name="Limit">The page size used.</param>
/// <param name="Offset">How many encounters were skipped.</param>
public sealed record EncounterListResponse(IReadOnlyList<EncounterResponse> Items, int Total, int Limit, int Offset);
