using Hl7.Fhir.Model;
using OpenEmrNext.ClinicalCare.Api.Fhir;
using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;
using Task = System.Threading.Tasks.Task;

namespace OpenEmrNext.ClinicalCare.Api.Endpoints;

/// <summary>
/// Read-only FHIR R4 <c>AllergyIntolerance</c>, <c>Condition</c> and <c>Encounter</c> (M5e, ADR 0079): <c>GET /fhir/{Type}/{id}</c> and <c>GET /fhir/{Type}?patient=...</c>. Each resource's file-level detail is its
/// mapper; the flow is <see cref="FhirEndpointKit"/>. The version is the record's stamp, as a weak <c>ETag</c> equal to the JSON API's and as <c>meta.versionId</c>, with the stamp as <c>meta.lastUpdated</c>.
/// </summary>
public static class FhirClinicalEndpoints
{
    private static readonly IReadOnlySet<string> NoExtraParameters = new HashSet<string>();
    private static readonly IReadOnlySet<string> CategoryParameter = new HashSet<string> { "category" };

    public static IEndpointRouteBuilder MapFhirClinicalEndpoints(this IEndpointRouteBuilder fhir)
    {
        MapAllergyIntolerance(fhir);
        MapCondition(fhir);
        MapEncounter(fhir);
        return fhir;
    }

    private static string? StampETag(DateTime? stamp) => stamp is null ? null : $"W/{ConcurrencyToken.From(stamp.Value).ToETag()}";

    private static void MapAllergyIntolerance(IEndpointRouteBuilder fhir)
    {
        const string type = "AllergyIntolerance";

        fhir.MapGet("/AllergyIntolerance/{id:guid}", (Guid id, GetAllergyUseCase get, CancellationToken ct) =>
                FhirEndpointKit.ReadAsync<AllergyView>(type, id, get.ExecuteAsync, (v, _) => Task.FromResult<IReadOnlyList<Resource>>(v.Select(x => (Resource)AllergyIntoleranceMapper.ToFhir(x)).ToList()), v => StampETag(v.LastModified), ct))
            .WithName("FhirReadAllergyIntolerance").WithTags("FHIR")
            .WithSummary("Read an allergy as a FHIR R4 AllergyIntolerance resource")
            .Produces<AllergyIntolerance>(contentType: FhirResult.ContentType)
            .Produces<OperationOutcome>(StatusCodes.Status404NotFound, FhirResult.ContentType);

        fhir.MapGet("/AllergyIntolerance", (HttpContext http, ListPatientAllergiesUseCase list, GetAllergyUseCase get, CancellationToken ct) =>
                FhirEndpointKit.SearchAsync<AllergyView>(
                    http, type, NoExtraParameters, FhirEndpointKit.NoExtra, get.ExecuteAsync, v => v.PatientUuid,
                    async (patient, count, offset, c) =>
                    {
                        try
                        {
                            var page = await list.ExecuteAsync(patient, count, offset, c);
                            return (page.Items, page.Total);
                        }
                        catch (PatientNotFoundException)
                        {
                            return ([], 0);
                        }
                    },
                    (v, _) => Task.FromResult<IReadOnlyList<Resource>>(v.Select(x => (Resource)AllergyIntoleranceMapper.ToFhir(x)).ToList()), ct))
            .WithName("FhirSearchAllergyIntolerances").WithTags("FHIR")
            .WithSummary("Search one patient's allergies (patient, _id, _count, _offset), as a FHIR searchset Bundle")
            .Produces<Bundle>(contentType: FhirResult.ContentType)
            .Produces<OperationOutcome>(StatusCodes.Status400BadRequest, FhirResult.ContentType);
    }

    private static void MapCondition(IEndpointRouteBuilder fhir)
    {
        const string type = "Condition";

        fhir.MapGet("/Condition/{id:guid}", (Guid id, GetProblemUseCase get, CancellationToken ct) =>
                FhirEndpointKit.ReadAsync<ProblemView>(type, id, get.ExecuteAsync, (v, _) => Task.FromResult<IReadOnlyList<Resource>>(v.Select(x => (Resource)ConditionMapper.ToFhir(x)).ToList()), v => StampETag(v.LastModified), ct))
            .WithName("FhirReadCondition").WithTags("FHIR")
            .WithSummary("Read a problem as a FHIR R4 Condition resource")
            .Produces<Condition>(contentType: FhirResult.ContentType)
            .Produces<OperationOutcome>(StatusCodes.Status404NotFound, FhirResult.ContentType);

        fhir.MapGet("/Condition", (HttpContext http, ListPatientProblemsUseCase list, GetProblemUseCase get, CancellationToken ct) =>
                FhirEndpointKit.SearchAsync<ProblemView>(
                    http, type, CategoryParameter, CheckCategory, get.ExecuteAsync, v => v.PatientUuid,
                    async (patient, count, offset, c) =>
                    {
                        try
                        {
                            var page = await list.ExecuteAsync(patient, count, offset, c);
                            return (page.Items, page.Total);
                        }
                        catch (PatientNotFoundException)
                        {
                            return ([], 0);
                        }
                    },
                    (v, _) => Task.FromResult<IReadOnlyList<Resource>>(v.Select(x => (Resource)ConditionMapper.ToFhir(x)).ToList()), ct))
            .WithName("FhirSearchConditions").WithTags("FHIR")
            .WithSummary("Search one patient's problem-list conditions (patient, _id, category, _count, _offset), as a FHIR searchset Bundle")
            .Produces<Bundle>(contentType: FhirResult.ContentType)
            .Produces<OperationOutcome>(StatusCodes.Status400BadRequest, FhirResult.ContentType);
    }

    /// <summary>Everything Clinical Care holds is a problem-list item, so that category is accepted (bare or with its system) and any other is refused, not answered with an empty set that looks like "none".</summary>
    private static (IResult? Refusal, IReadOnlyDictionary<string, string> Filters) CheckCategory(IQueryCollection query)
    {
        if (!query.TryGetValue("category", out var values))
            return (null, new Dictionary<string, string>());

        var value = values[0]!;
        var code = value.Contains('|') ? value[(value.IndexOf('|') + 1)..] : value;
        return code == ConditionMapper.ProblemListItem
            ? (null, new Dictionary<string, string> { ["category"] = value })
            : (FhirOutcomes.Invalid($"The search parameter 'category' for Condition supports only '{ConditionMapper.ProblemListItem}': that is the only kind of condition Clinical Care holds."), new Dictionary<string, string>());
    }

    private static void MapEncounter(IEndpointRouteBuilder fhir)
    {
        const string type = "Encounter";

        fhir.MapGet("/Encounter/{id:guid}", (Guid id, GetEncounterUseCase get, EncounterLookupsUseCase lookups, IUserDirectory users, CancellationToken ct) =>
                FhirEndpointKit.ReadAsync<EncounterView>(type, id, get.ExecuteAsync, (v, c) => MapAsync(v, lookups, users, c), v => StampETag(v.LastUpdate), ct))
            .WithName("FhirReadEncounter").WithTags("FHIR")
            .WithSummary("Read an encounter as a FHIR R4 Encounter resource")
            .Produces<Encounter>(contentType: FhirResult.ContentType)
            .Produces<OperationOutcome>(StatusCodes.Status404NotFound, FhirResult.ContentType);

        fhir.MapGet("/Encounter", (HttpContext http, ListPatientEncountersUseCase list, GetEncounterUseCase get, EncounterLookupsUseCase lookups, IUserDirectory users, CancellationToken ct) =>
                FhirEndpointKit.SearchAsync<EncounterView>(
                    http, type, NoExtraParameters, FhirEndpointKit.NoExtra, get.ExecuteAsync, v => v.PatientUuid,
                    async (patient, count, offset, c) =>
                    {
                        try
                        {
                            var page = await list.ExecuteAsync(patient, count, offset, c);
                            return (page.Items, page.Total);
                        }
                        catch (PatientNotFoundException)
                        {
                            return ([], 0);
                        }
                    },
                    (v, c) => MapAsync(v, lookups, users, c), ct))
            .WithName("FhirSearchEncounters").WithTags("FHIR")
            .WithSummary("Search one patient's encounters (patient, _id, _count, _offset), as a FHIR searchset Bundle")
            .Produces<Bundle>(contentType: FhirResult.ContentType)
            .Produces<OperationOutcome>(StatusCodes.Status400BadRequest, FhirResult.ContentType);
    }

    private static async Task<IReadOnlyList<Resource>> MapAsync(IReadOnlyList<EncounterView> views, EncounterLookupsUseCase lookups, IUserDirectory users, CancellationToken ct)
    {
        var categories = (await lookups.CalendarCategoriesAsync(ct)).ToDictionary(c => c.Id, c => c.Name);
        var people = await users.FindPeopleAsync(views.Select(v => v.ProviderId).Where(i => i > 0).ToList(), ct);
        return views.Select(v => (Resource)EncounterMapper.ToFhir(v, categories, people)).ToList();
    }
}
