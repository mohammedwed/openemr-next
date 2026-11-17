using Hl7.Fhir.Model;
using OpenEmrNext.ClinicalCare.Api.Fhir;
using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;
using Task = System.Threading.Tasks.Task;

namespace OpenEmrNext.ClinicalCare.Api.Endpoints;

/// <summary>
/// Read-only FHIR R4 vital-sign <c>Observation</c> (M5e, ADR 0079): <c>GET /fhir/Observation/{id}</c> and <c>GET /fhir/Observation?patient=...</c>. One vitals set is up to ten Observations, so a search counts and pages over
/// the <i>Observations</i>, not the sets: the patient's most recent 1000 sets are read, expanded, and then sliced. The id is <c>{vitalsUuid}-{measurement}</c> (see <see cref="ObservationMapper"/>).
/// </summary>
public static class FhirObservationEndpoints
{
    private const string Type = "Observation";
    private static readonly IReadOnlySet<string> CategoryParameter = new HashSet<string> { "category" };

    public static IEndpointRouteBuilder MapFhirObservationEndpoints(this IEndpointRouteBuilder fhir)
    {
        fhir.MapGet("/Observation/{id}", Read)
            .WithName("FhirReadObservation").WithTags("FHIR")
            .WithSummary("Read one vital-sign measurement as a FHIR R4 Observation resource (id: the vitals uuid, a dash, the measurement)")
            .Produces<Observation>(contentType: FhirResult.ContentType)
            .Produces<OperationOutcome>(StatusCodes.Status404NotFound, FhirResult.ContentType);

        fhir.MapGet("/Observation", Search)
            .WithName("FhirSearchObservations").WithTags("FHIR")
            .WithSummary("Search one patient's vital-sign Observations (patient, _id, category, _count, _offset), as a FHIR searchset Bundle")
            .Produces<Bundle>(contentType: FhirResult.ContentType)
            .Produces<OperationOutcome>(StatusCodes.Status400BadRequest, FhirResult.ContentType);

        return fhir;
    }

    private static async Task<IResult> Read(string id, FindVitalsAnywhereUseCase find, CancellationToken ct)
    {
        if (!ObservationMapper.TryParseId(id, out var vitalsUuid, out var slug))
            return FhirOutcomes.NotFound(Type, id);

        var set = await find.ExecuteAsync(vitalsUuid, ct);
        var observation = set is null ? null : ObservationMapper.One(set, slug);
        return observation is null || set is null
            ? FhirOutcomes.NotFound(Type, id)
            : new FhirResult(observation, weakETag: set.Vitals.LastUpdated is { } stamp ? $"W/{ConcurrencyToken.From(stamp).ToETag()}" : null);
    }

    private static async Task<IResult> Search(HttpContext http, ListPatientVitalsUseCase list, FindVitalsAnywhereUseCase find, CancellationToken ct)
    {
        if (!FhirSearchParser.TryParse(Type, http.Request.Query, CategoryParameter, out var search, out var refusal, idValid: id => ObservationMapper.TryParseId(id, out _, out _)))
            return refusal;

        string? category = null;
        if (http.Request.Query.TryGetValue("category", out var c))
        {
            category = c[0]!;
            var code = category.Contains('|') ? category[(category.IndexOf('|') + 1)..] : category;
            if (code != ObservationMapper.VitalSigns)
                return FhirOutcomes.Invalid($"The search parameter 'category' for Observation supports only '{ObservationMapper.VitalSigns}': those are the only observations Clinical Care holds.");
        }

        IReadOnlyList<Observation> all;
        if (search.IdText is { } idText)
        {
            ObservationMapper.TryParseId(idText, out var vitalsUuid, out var slug);
            var set = await find.ExecuteAsync(vitalsUuid, ct);
            var one = set is null || (search.Patient is not null && set.PatientUuid != search.Patient) ? null : ObservationMapper.One(set, slug);
            all = one is null ? [] : [one];
        }
        else
        {
            try
            {
                all = (await list.ExecuteAsync(search.Patient!.Value, ct)).SelectMany(ObservationMapper.ToFhir).ToList();
            }
            catch (PatientNotFoundException)
            {
                all = [];   // FHIR answers a search for a patient that is not there with an empty set, not an error
            }
        }

        var page = all.Skip(search.Offset).Take(search.Count).Cast<Resource>().ToList();
        var filters = new Dictionary<string, string>();
        if (search.Patient is { } p) filters["patient"] = p.ToString();
        if (search.IdText is { } i) filters["_id"] = i;
        if (category is not null) filters["category"] = category;

        return new FhirResult(FhirBundles.SearchSet(http.Request, Type, page, all.Count, search, filters));
    }
}
