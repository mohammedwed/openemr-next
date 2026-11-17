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

/// <summary>One choice in a configured list: the <paramref name="Id"/> to send and the <paramref name="Title"/> to show.</summary>
public sealed record LookupOptionResponse(string Id, string Title);

/// <summary>The choices for an immunization's coded fields, the active options in legacy's order. A vaccine's id is a whole number as text; send it as <c>immunizationId</c>.</summary>
public sealed record ImmunizationOptionsResponse(
    IReadOnlyList<LookupOptionResponse> Vaccines, IReadOnlyList<LookupOptionResponse> Routes, IReadOnlyList<LookupOptionResponse> Sites,
    IReadOnlyList<LookupOptionResponse> CompletionStatuses, IReadOnlyList<LookupOptionResponse> InformationSources, IReadOnlyList<LookupOptionResponse> RefusalReasons,
    IReadOnlyList<LookupOptionResponse> Units);
