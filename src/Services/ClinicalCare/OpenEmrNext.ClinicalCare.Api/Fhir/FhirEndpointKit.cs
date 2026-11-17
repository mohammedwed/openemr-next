using Hl7.Fhir.Model;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using Task = System.Threading.Tasks.Task;

namespace OpenEmrNext.ClinicalCare.Api.Fhir;

/// <summary>
/// The read and search flow every clinical FHIR resource shares (ADR 0079), so each resource's endpoint file says only what is particular to it: how to find one, how to list a patient's, how to map, and any
/// extra search parameter. One place decides that a read of a missing id is a 404 <c>OperationOutcome</c>, that a search by id is limited to the patient named, that a patient with nothing is an empty set, and what the Bundle looks like.
/// </summary>
internal static class FhirEndpointKit
{
    public static async Task<IResult> ReadAsync<TView>(
        string type, Guid id, Func<Guid, CancellationToken, Task<TView?>> find, Func<IReadOnlyList<TView>, CancellationToken, Task<IReadOnlyList<Resource>>> map,
        Func<TView, string?> weakETag, CancellationToken ct)
        where TView : class
    {
        var view = await find(id, ct);
        if (view is null)
            return FhirOutcomes.NotFound(type, id);

        var resource = await map([view], ct);
        return new FhirResult(resource[0], weakETag: weakETag(view));
    }

    /// <param name="extraParameters">The parameters this resource adds to <c>patient</c>, <c>_id</c>, <c>_count</c> and <c>_offset</c>.</param>
    /// <param name="checkExtra">Judges those extra parameters: a ready 400 to refuse one, or <c>null</c> to accept. Anything it accepts is carried into the Bundle's links.</param>
    /// <param name="list">One page of a patient's records, newest first, and the total, within the <c>_lastUpdated</c> window when there is one; throws nothing for a patient that is not there (the caller turns that into an empty set).</param>
    /// <param name="stampOf">A record's last-changed stamp. When given, <c>_lastUpdated</c> is a supported search parameter; when <c>null</c> (Immunization has no stamp) it is refused.</param>
    public static async Task<IResult> SearchAsync<TView>(
        HttpContext http, string type, IReadOnlySet<string> extraParameters, Func<IQueryCollection, (IResult? Refusal, IReadOnlyDictionary<string, string> Filters)> checkExtra,
        Func<Guid, CancellationToken, Task<TView?>> find, Func<TView, Guid?> patientOf,
        Func<Guid, int, int, UpdatedWindow?, CancellationToken, Task<(IReadOnlyList<TView> Items, int Total)>> list,
        Func<IReadOnlyList<TView>, CancellationToken, Task<IReadOnlyList<Resource>>> map, Func<TView, DateTime?>? stampOf, CancellationToken ct)
        where TView : class
    {
        if (!FhirSearchParser.TryParse(type, http.Request.Query, extraParameters, out var search, out var refusal, lastUpdated: stampOf is not null))
            return refusal;

        var (extraRefusal, extraFilters) = checkExtra(http.Request.Query);
        if (extraRefusal is not null)
            return extraRefusal;

        IReadOnlyList<TView> found;
        int total;
        if (search.Id is { } id)
        {
            // By id: at most one, and when a patient is also named it has to be that patient's.
            var one = await find(id, ct);
            found = one is not null && (search.Patient is null || patientOf(one) == search.Patient) && (search.Updated is null || search.Updated.Contains(stampOf!(one))) ? [one] : [];
            total = found.Count;
        }
        else
        {
            (found, total) = await list(search.Patient!.Value, search.Count, search.Offset, search.Updated, ct);
        }

        var filters = new List<KeyValuePair<string, string>>();
        if (search.Patient is { } p) filters.Add(new("patient", p.ToString()));
        if (search.Id is { } i) filters.Add(new("_id", i.ToString()));
        foreach (var (name, value) in extraFilters) filters.Add(new(name, value));
        foreach (var stamp in search.LastUpdatedAsSent ?? []) filters.Add(new("_lastUpdated", stamp));

        var resources = await map(found, ct);
        return new FhirResult(FhirBundles.SearchSet(http.Request, type, resources, total, search, filters));
    }

    /// <summary>No extra parameters to judge.</summary>
    public static (IResult? Refusal, IReadOnlyDictionary<string, string> Filters) NoExtra(IQueryCollection _) => (null, new Dictionary<string, string>());
}
