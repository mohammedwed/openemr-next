using OpenEmrNext.ClinicalCare.Api.Contracts.Responses;
using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;

namespace OpenEmrNext.ClinicalCare.Api.Endpoints;

/// <summary>
/// Read-only lists of the choices an encounter needs (M5d slice 4, decision 10 of docs/patient-view-pending-decisions.md). Each is small configuration and returned whole. Providers are offered (M5f) only to choose someone
/// other than the signed-in user; the acting provider still comes from the session (ADR 0075).
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

        app.MapGet("/lookups/providers", async (EncounterLookupsUseCase useCase, CancellationToken ct) =>
                Results.Ok((await useCase.ProvidersAsync(ct)).Select(p => new ProviderLookupResponse(p.Id, p.DisplayName)).ToList()))
            .WithName("ListProviders").WithTags("Lookups")
            .WithSummary("List the active providers (users legacy marks authorized), by name")
            .Produces<List<ProviderLookupResponse>>();

        app.MapGet("/lookups/immunization-options", async (IImmunizationLookups lookups, CancellationToken ct) =>
            {
                var o = await lookups.GetOptionsAsync(ct);
                static List<LookupOptionResponse> Map(IReadOnlyList<LookupOption> list) => list.Select(x => new LookupOptionResponse(x.Id, x.Title)).ToList();
                return Results.Ok(new ImmunizationOptionsResponse(
                    Map(o.Vaccines), Map(o.Routes), Map(o.Sites), Map(o.CompletionStatuses), Map(o.InformationSources), Map(o.RefusalReasons), Map(o.Units)));
            })
            .WithName("GetImmunizationOptions").WithTags("Lookups")
            .WithSummary("List the active choices for an immunization's coded fields (vaccine, route, site, status, source, refusal reason, unit)")
            .Produces<ImmunizationOptionsResponse>();

        app.MapGet("/lookups/clinical-note-types", async (ClinicalNoteLookupsUseCase useCase, CancellationToken ct) =>
                Results.Ok((await useCase.TypesAsync(ct)).Select(t => new ClinicalNoteTypeLookupResponse(t.Id, t.Title, t.Code)).ToList()))
            .WithName("ListClinicalNoteTypes").WithTags("Lookups")
            .WithSummary("List the active clinical note types, in legacy's order")
            .Produces<List<ClinicalNoteTypeLookupResponse>>();

        app.MapGet("/lookups/clinical-note-categories", async (ClinicalNoteLookupsUseCase useCase, CancellationToken ct) =>
                Results.Ok((await useCase.CategoriesAsync(ct)).Select(c => new ClinicalNoteCategoryLookupResponse(c.Id, c.Title)).ToList()))
            .WithName("ListClinicalNoteCategories").WithTags("Lookups")
            .WithSummary("List the active clinical note categories, in legacy's order")
            .Produces<List<ClinicalNoteCategoryLookupResponse>>();

        return app;
    }
}
