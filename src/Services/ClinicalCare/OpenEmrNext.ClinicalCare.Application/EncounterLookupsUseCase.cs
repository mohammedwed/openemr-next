using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Application;

/// <summary>The choices for filling in an encounter (M5d slice 4): classes, facilities, visit categories and providers. Thin by design (ARC-4): the ports hold the coupling to legacy configuration.</summary>
public sealed class EncounterLookupsUseCase(IEncounterCodeLists classes, IEncounterLookups lookups)
{
    public Task<IReadOnlyList<EncounterClassOption>> ClassesAsync(CancellationToken ct) => classes.GetEncounterClassesAsync(ct);

    public Task<IReadOnlyList<FacilityLookup>> FacilitiesAsync(CancellationToken ct) => lookups.GetFacilitiesAsync(ct);

    public Task<IReadOnlyList<CalendarCategoryLookup>> CalendarCategoriesAsync(CancellationToken ct) => lookups.GetCalendarCategoriesAsync(ct);

    public Task<IReadOnlyList<ProviderLookup>> ProvidersAsync(CancellationToken ct) => lookups.GetProvidersAsync(ct);
}
