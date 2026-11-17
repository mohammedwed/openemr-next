using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;

namespace OpenEmrNext.ClinicalCare.Api.Fhir;

/// <summary>A search the service understood: whose records, which one, and which page.</summary>
internal sealed record FhirSearch(Guid? Patient, Guid? Id, int Count, int Offset);

/// <summary>
/// Reads a FHIR search (ADR 0079 #6): a small, strict set of parameters. <c>patient</c> (a patient uuid, bare or as <c>Patient/{uuid}</c>), <c>_id</c>, <c>_count</c> (default 20, at most 100) and <c>_offset</c> for paging,
/// plus whatever a resource adds. Anything else is refused, not ignored: a clinical search that quietly drops a filter returns the wrong set. A repeated parameter is refused too, because which value counts is a guess.
/// </summary>
internal static class FhirSearchParser
{
    public const int DefaultCount = 20, MaxCount = 100;

    /// <param name="resourceType">For the message.</param>
    /// <param name="extraParameters">The parameters this resource adds to the common ones.</param>
    /// <returns>The search, or a ready 400 answer in <paramref name="refusal"/>.</returns>
    public static bool TryParse(string resourceType, IQueryCollection query, IReadOnlySet<string> extraParameters, out FhirSearch search, out IResult refusal)
    {
        search = new FhirSearch(null, null, DefaultCount, 0);
        refusal = Results.Empty;
        var supported = new HashSet<string>(["patient", "_id", "_count", "_offset"], StringComparer.Ordinal);
        supported.UnionWith(extraParameters);

        foreach (var (name, values) in query)
        {
            if (!supported.Contains(name))
            {
                refusal = FhirOutcomes.Invalid($"The search parameter '{name}' is not supported for {resourceType}. Supported: {string.Join(", ", supported.Order(StringComparer.Ordinal))}.");
                return false;
            }

            if (values.Count != 1 || string.IsNullOrWhiteSpace(values[0]))
            {
                refusal = FhirOutcomes.Invalid($"The search parameter '{name}' must be given exactly once, with a value.");
                return false;
            }
        }

        Guid? patient = null, id = null;
        if (query.TryGetValue("patient", out var p))
        {
            var text = p[0]!.StartsWith("Patient/", StringComparison.Ordinal) ? p[0]![8..] : p[0]!;
            if (!Guid.TryParse(text, out var parsed) || parsed == Guid.Empty)
            {
                refusal = FhirOutcomes.Invalid("The search parameter 'patient' must be a patient's id, or a reference of the form Patient/{id}.");
                return false;
            }

            patient = parsed;
        }

        if (query.TryGetValue("_id", out var i))
        {
            if (!Guid.TryParse(i[0], out var parsed) || parsed == Guid.Empty)
            {
                refusal = FhirOutcomes.Invalid("The search parameter '_id' must be a resource id.");
                return false;
            }

            id = parsed;
        }

        if (patient is null && id is null)
        {
            refusal = FhirOutcomes.Invalid($"A search for {resourceType} needs 'patient' (or '_id'): Clinical Care does not return every patient's records at once.");
            return false;
        }

        var count = DefaultCount;
        if (query.TryGetValue("_count", out var c) && (!int.TryParse(c[0], out count) || count is < 1 or > MaxCount))
        {
            refusal = FhirOutcomes.Invalid($"The search parameter '_count' must be a whole number from 1 to {MaxCount}.");
            return false;
        }

        var offset = 0;
        if (query.TryGetValue("_offset", out var o) && (!int.TryParse(o[0], out offset) || offset < 0))
        {
            refusal = FhirOutcomes.Invalid("The search parameter '_offset' must be a whole number, 0 or more.");
            return false;
        }

        search = new FhirSearch(patient, id, count, offset);
        return true;
    }
}

/// <summary>Builds the <c>searchset</c> <c>Bundle</c> every search answers with: the total, a self link, and a next link when there is more.</summary>
internal static class FhirBundles
{
    public static Bundle SearchSet(HttpRequest request, string resourceType, IReadOnlyList<Resource> page, int total, FhirSearch search, IReadOnlyDictionary<string, string> filters)
    {
        var baseUrl = BaseUrl(request);
        var bundle = new Bundle { Type = Bundle.BundleType.Searchset, Total = total };

        bundle.Link.Add(new Bundle.LinkComponent { Relation = "self", Url = Url(baseUrl, resourceType, filters, search.Count, search.Offset) });
        if (search.Offset + page.Count < total)
            bundle.Link.Add(new Bundle.LinkComponent { Relation = "next", Url = Url(baseUrl, resourceType, filters, search.Count, search.Offset + search.Count) });

        foreach (var resource in page)
        {
            bundle.Entry.Add(new Bundle.EntryComponent
            {
                FullUrl = $"{baseUrl}/fhir/{resourceType}/{resource.Id}",
                Resource = resource,
                Search = new Bundle.SearchComponent { Mode = Bundle.SearchEntryMode.Match }
            });
        }

        return bundle;
    }

    /// <summary>The address the caller used, not the one the gateway forwarded to: the proto and host the gateway saw.</summary>
    public static string BaseUrl(HttpRequest request)
    {
        var scheme = request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? request.Scheme;
        var host = request.Headers["X-Forwarded-Host"].FirstOrDefault() ?? request.Host.Value;
        return $"{scheme}://{host}";
    }

    private static string Url(string baseUrl, string resourceType, IReadOnlyDictionary<string, string> filters, int count, int offset) =>
        $"{baseUrl}/fhir/{resourceType}?" + string.Join('&', filters.Select(f => $"{f.Key}={Uri.EscapeDataString(f.Value)}").Append($"_count={count}").Append($"_offset={offset}"));
}
