using OpenEmrNext.ClinicalCare.Api.Contracts.Responses;
using OpenEmrNext.ClinicalCare.Application;

namespace OpenEmrNext.ClinicalCare.Api.Endpoints;

/// <summary>
/// Read-only lists of the choices an encounter needs (M5d slice 4, decision 10 of docs/patient-view-pending-decisions.md). Each is small configuration and returned whole. Providers are not offered: they are staff names,
/// and the acting provider comes from the session once identity is forwarded (M5b phase 1b).
/// </summary>
public static class LookupEndpoints
{
    public static IEndpointRouteBuilder MapLookupEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/lookups/encounter-classes", async (EncounterLookupsUseCase useCase, CancellationToken ct) =>
                Results.Ok((await useCase.ClassesAsync(ct)).Select(c => new EncounterClassLookupResponse(c.Code, c.Title, c.IsDefault)).ToList()))
            .WithName("ListEncounterClasses").WithTags("Lookups")
            .WithSummary("List the encounter classes (legacy _ActEncounterCode), in legacy's order")
            .Produces<List<EncounterClassLookupResponse>>();

        app.MapGet("/lookups/facilities", async (EncounterLookupsUseCase useCase, CancellationToken ct) =>
                Results.Ok((await useCase.FacilitiesAsync(ct)).Select(f => new FacilityLookupResponse(f.Id, f.Name, f.PosCode, f.IsServiceLocation)).ToList()))
            .WithName("ListFacilities").WithTags("Lookups")
            .WithSummary("List the active facilities, by name")
            .Produces<List<FacilityLookupResponse>>();

        app.MapGet("/lookups/calendar-categories", async (EncounterLookupsUseCase useCase, CancellationToken ct) =>
                Results.Ok((await useCase.CalendarCategoriesAsync(ct)).Select(c => new CalendarCategoryLookupResponse(c.Id, c.Name)).ToList()))
            .WithName("ListCalendarCategories").WithTags("Lookups")
            .WithSummary("List the active visit categories, as legacy's visit form offers them")
            .Produces<List<CalendarCategoryLookupResponse>>();

        return app;
    }
}
