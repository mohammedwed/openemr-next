using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;
using OpenEmrNext.ClinicalCare.Api.Fhir;
using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;

namespace OpenEmrNext.ClinicalCare.Api.Endpoints;

/// <summary>
/// Read-only FHIR R4 <c>Immunization</c> (M5e, ADR 0079): <c>GET /fhir/Immunization/{id}</c> and <c>GET /fhir/Immunization?patient=...</c>. The same sign-in as every route (ADR 0075): the gateway has verified the
/// session and the middleware has resolved the user before this runs. Writes stay on the JSON API.
/// </summary>
public static class FhirImmunizationEndpoints
{
    private const string Type = "Immunization";
    private static readonly IReadOnlySet<string> NoExtraParameters = new HashSet<string>();

    public static IEndpointRouteBuilder MapFhirImmunizationEndpoints(this IEndpointRouteBuilder fhir)
    {
        fhir.MapGet("/Immunization/{id:guid}", Read)
            .WithName("FhirReadImmunization").WithTags("FHIR")
            .WithSummary("Read an immunization as a FHIR R4 Immunization resource")
            .Produces<Immunization>(contentType: FhirResult.ContentType)
            .Produces<OperationOutcome>(StatusCodes.Status404NotFound, FhirResult.ContentType);

        fhir.MapGet("/Immunization", Search)
            .WithName("FhirSearchImmunizations").WithTags("FHIR")
            .WithSummary("Search one patient's immunizations (patient, _id, _count, _offset), as a FHIR searchset Bundle")
            .Produces<Bundle>(contentType: FhirResult.ContentType)
            .Produces<OperationOutcome>(StatusCodes.Status400BadRequest, FhirResult.ContentType);

        return fhir;
    }

    private static async Task<IResult> Read(Guid id, GetImmunizationUseCase useCase, IUserDirectory users, CancellationToken ct)
    {
        var view = await useCase.ExecuteAsync(id, ct);
        if (view is null)
            return FhirOutcomes.NotFound(Type, id);

        var resource = await MapAsync([view], users, ct);
        return new FhirResult(resource[0], weakETag: $"W/{view.Version().ToETag()}");
    }

    private static async Task<IResult> Search(
        HttpContext http, ListPatientImmunizationsUseCase list, GetImmunizationUseCase get, IUserDirectory users, CancellationToken ct)
    {
        if (!FhirSearchParser.TryParse(Type, http.Request.Query, NoExtraParameters, out var search, out var refusal))
            return refusal;

        IReadOnlyList<ImmunizationView> found;
        int total;
        if (search.Id is { } id)
        {
            // By id: at most one, and when a patient is also named it has to be that patient's.
            var one = await get.ExecuteAsync(id, ct);
            found = one is not null && (search.Patient is null || one.PatientUuid == search.Patient) ? [one] : [];
            total = found.Count;
        }
        else
        {
            try
            {
                var page = await list.ExecuteAsync(search.Patient!.Value, search.Count, search.Offset, ct);
                found = page.Items;
                total = page.Total;
            }
            catch (PatientNotFoundException)
            {
                (found, total) = ([], 0);   // FHIR answers a search for a patient that is not there with an empty set, not an error
            }
        }

        var filters = new Dictionary<string, string>();
        if (search.Patient is { } p) filters["patient"] = p.ToString();
        if (search.Id is { } i) filters["_id"] = i.ToString();

        var resources = await MapAsync(found, users, ct);
        return new FhirResult(FhirBundles.SearchSet(http.Request, Type, resources, total, search, filters));
    }

    private static async Task<IReadOnlyList<Resource>> MapAsync(IReadOnlyList<ImmunizationView> views, IUserDirectory users, CancellationToken ct)
    {
        var ids = views.SelectMany(v => new[] { v.AdministeredById, v.OrderingProvider }).Where(i => i is > 0).Select(i => i!.Value).ToList();
        var practitioners = await users.FindUuidsAsync(ids, ct);
        var offset = TimeZoneInfo.Local;
        return views.Select(v => (Resource)ImmunizationMapper.ToFhir(v, practitioners, offset.GetUtcOffset(v.AdministeredDate ?? DateTime.UtcNow))).ToList();
    }
}
