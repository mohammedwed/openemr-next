using OpenEmrNext.ClinicalCare.Domain.ReadModels;

namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>
/// The choices a caller needs to fill in an encounter (M5d slice 4): facilities and visit categories. Classes come from <see cref="IEncounterCodeLists"/>. Providers are deliberately absent: they are staff names,
/// and the acting provider should come from the session once identity is forwarded (decision 10 in docs/patient-view-pending-decisions.md).
/// </summary>
public interface IEncounterLookups
{
    /// <returns>The active facilities (legacy's <c>inactive = 0</c>), by name.</returns>
    Task<IReadOnlyList<FacilityLookup>> GetFacilitiesAsync(CancellationToken ct);

    /// <returns>The active patient-visit categories in legacy's own order, as legacy's visit form offers them.</returns>
    Task<IReadOnlyList<CalendarCategoryLookup>> GetCalendarCategoriesAsync(CancellationToken ct);
}
