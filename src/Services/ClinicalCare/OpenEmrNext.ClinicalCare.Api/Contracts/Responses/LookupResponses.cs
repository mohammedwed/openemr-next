namespace OpenEmrNext.ClinicalCare.Api.Contracts.Responses;

/// <summary>An encounter class a caller can choose, as <c>classCode</c> when creating or updating an encounter.</summary>
/// <param name="Code">The code to send (for example <c>AMB</c>).</param>
/// <param name="Title">The label to show.</param>
/// <param name="IsDefault">Whether legacy preselects it.</param>
public sealed record EncounterClassLookupResponse(string Code, string Title, bool IsDefault);

/// <summary>A facility a caller can choose, as <c>facilityId</c>. <paramref name="PosCode"/> is the place of service an encounter takes from it when none is given.</summary>
public sealed record FacilityLookupResponse(int Id, string Name, int? PosCode, bool IsServiceLocation);

/// <summary>A visit category a caller can choose, as <c>calendarCategoryId</c>.</summary>
public sealed record CalendarCategoryLookupResponse(int Id, string Name);
