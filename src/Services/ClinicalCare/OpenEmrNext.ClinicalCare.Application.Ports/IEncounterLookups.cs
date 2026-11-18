using OpenEmrNext.ClinicalCare.Domain.ReadModels;

namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>
/// The choices a caller needs to fill in an encounter (M5d slice 4): facilities and visit categories, and (M5f) the providers an encounter can name. Classes come from <see cref="IEncounterCodeLists"/>. The acting
/// provider still comes from the session (ADR 0075); the providers list is only for choosing someone else when recording on their behalf.
/// </summary>
public interface IEncounterLookups
{
    /// <returns>The active facilities (legacy's <c>inactive = 0</c>), by name.</returns>
    Task<IReadOnlyList<FacilityLookup>> GetFacilitiesAsync(CancellationToken ct);

    /// <returns>The active patient-visit categories in legacy's own order, as legacy's visit form offers them.</returns>
    Task<IReadOnlyList<CalendarCategoryLookup>> GetCalendarCategoriesAsync(CancellationToken ct);

    /// <returns>The active users legacy treats as providers (<c>authorized = 1</c>), by last name then first name.</returns>
    Task<IReadOnlyList<ProviderLookup>> GetProvidersAsync(CancellationToken ct);
}
