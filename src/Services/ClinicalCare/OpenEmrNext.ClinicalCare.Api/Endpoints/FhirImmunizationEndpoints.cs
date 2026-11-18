using Hl7.Fhir.Model;
using OpenEmrNext.ClinicalCare.Api.Fhir;
using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using Task = System.Threading.Tasks.Task;

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
        fhir.MapGet("/Immunization/{id:guid}", (Guid id, GetImmunizationUseCase get, IUserDirectory users, CancellationToken ct) =>
                FhirEndpointKit.ReadAsync<ImmunizationView>(Type, id, get.ExecuteAsync, (v, c) => MapAsync(v, users, c), v => $"W/{v.Version().ToETag()}", ct))
            .WithName("FhirReadImmunization").WithTags("FHIR")
            .WithSummary("Read an immunization as a FHIR R4 Immunization resource")
            .Produces<Immunization>(contentType: FhirResult.ContentType)
            .Produces<OperationOutcome>(StatusCodes.Status404NotFound, FhirResult.ContentType);

        fhir.MapGet("/Immunization", (HttpContext http, ListPatientImmunizationsUseCase list, GetImmunizationUseCase get, IUserDirectory users, CancellationToken ct) =>
                FhirEndpointKit.SearchAsync<ImmunizationView>(
                    http, Type, NoExtraParameters, FhirEndpointKit.NoExtra, get.ExecuteAsync, v => v.PatientUuid,
                    async (patient, count, offset, _, c) =>
                    {
                        try
                        {
                            var page = await list.ExecuteAsync(patient, count, offset, c);
                            return (page.Items, page.Total);
                        }
                        catch (PatientNotFoundException)
                        {
                            return ([], 0);   // FHIR answers a search for a patient that is not there with an empty set, not an error
                        }
                    },
                    (v, c) => MapAsync(v, users, c), stampOf: null, ct))
            .WithName("FhirSearchImmunizations").WithTags("FHIR")
            .WithSummary("Search one patient's immunizations (patient, _id, _count, _offset), as a FHIR searchset Bundle")
            .Produces<Bundle>(contentType: FhirResult.ContentType)
            .Produces<OperationOutcome>(StatusCodes.Status400BadRequest, FhirResult.ContentType);

        return fhir;
    }

    private static async Task<IReadOnlyList<Resource>> MapAsync(IReadOnlyList<ImmunizationView> views, IUserDirectory users, CancellationToken ct)
    {
        var ids = views.SelectMany(v => new[] { v.AdministeredById, v.OrderingProvider }).Where(i => i is > 0).Select(i => i!.Value).ToList();
        var practitioners = await users.FindUuidsAsync(ids, ct);
        var offset = TimeZoneInfo.Local;
        return views.Select(v => (Resource)ImmunizationMapper.ToFhir(v, practitioners, offset.GetUtcOffset(v.AdministeredDate ?? DateTime.UtcNow))).ToList();
    }
}
