using Hl7.Fhir.Model;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using Task = System.Threading.Tasks.Task;

namespace OpenEmrNext.ClinicalCare.Api.Fhir;

/// <summary>A search the service understood: whose records, which one, and which page.</summary>
/// <param name="IdText">The <c>_id</c> as sent, for a resource whose id is not a bare uuid (an Observation's is <c>{uuid}-{measurement}</c>); <paramref name="Id"/> is then <c>null</c>.</param>
/// <param name="Updated">The window the <c>_lastUpdated</c> parameter(s) describe, or <c>null</c> when there were none.</param>
/// <param name="LastUpdatedAsSent">Each <c>_lastUpdated</c> value as it was sent, so the Bundle's links can repeat it.</param>
internal sealed record FhirSearch(Guid? Patient, Guid? Id, int Count, int Offset, string? IdText = null, UpdatedWindow? Updated = null, IReadOnlyList<string>? LastUpdatedAsSent = null);

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
    /// <param name="idValid">For a resource whose id is not a bare uuid: judges the <c>_id</c> text. Left <c>null</c>, <c>_id</c> must be a uuid.</param>
    public static bool TryParse(
        string resourceType, IQueryCollection query, IReadOnlySet<string> extraParameters, out FhirSearch search, out IResult refusal, Func<string, bool>? idValid = null, bool lastUpdated = false)
    {
        search = new FhirSearch(null, null, DefaultCount, 0);
        refusal = Results.Empty;
        var supported = new HashSet<string>(["patient", "_id", "_count", "_offset"], StringComparer.Ordinal);
        supported.UnionWith(extraParameters);
        if (lastUpdated)
            supported.Add("_lastUpdated");

        foreach (var (name, values) in query)
        {
            if (!supported.Contains(name))
            {
                refusal = FhirOutcomes.Invalid($"The search parameter '{name}' is not supported for {resourceType}. Supported: {string.Join(", ", supported.Order(StringComparer.Ordinal))}.");
                return false;
            }

            // _lastUpdated may be given twice, as the two ends of a range; everything else exactly once.
            var allowed = name == "_lastUpdated" ? 2 : 1;
            if (values.Count < 1 || values.Count > allowed || values.Any(string.IsNullOrWhiteSpace))
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

        string? idText = null;
        if (query.TryGetValue("_id", out var i))
        {
            if (idValid is not null)
            {
                if (!idValid(i[0]!))
                {
                    refusal = FhirOutcomes.Invalid("The search parameter '_id' must be a resource id.");
                    return false;
                }

                idText = i[0];
            }
            else if (!Guid.TryParse(i[0], out var parsed) || parsed == Guid.Empty)
            {
                refusal = FhirOutcomes.Invalid("The search parameter '_id' must be a resource id.");
                return false;
            }
            else
            {
                id = parsed;
            }
        }

        if (patient is null && id is null && idText is null)
        {
            refusal = FhirOutcomes.Invalid($"A search for {resourceType} needs 'patient' (or '_id'): Clinical Care does not return every patient's records at once.");
            return false;
        }

        UpdatedWindow? window = null;
        IReadOnlyList<string>? sent = null;
        if (query.TryGetValue("_lastUpdated", out var stamps))
        {
            foreach (var value in stamps)
            {
                if (!FhirDateSearch.TryParse(value!, out var one, out var problem))
                {
                    refusal = FhirOutcomes.Invalid($"The search parameter '_lastUpdated': {problem}");
                    return false;
                }

                window = window is null ? one : window.Intersect(one);
            }

            sent = stamps.Select(v => v!).ToList();
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

        search = new FhirSearch(patient, id, count, offset, idText, window, sent);
        return true;
    }
}

/// <summary>Builds the <c>searchset</c> <c>Bundle</c> every search answers with: the total, a self link, and a next link when there is more.</summary>
internal static class FhirBundles
{
    public static Bundle SearchSet(HttpRequest request, string resourceType, IReadOnlyList<Resource> page, int total, FhirSearch search, IEnumerable<KeyValuePair<string, string>> filters)
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

    private static string Url(string baseUrl, string resourceType, IEnumerable<KeyValuePair<string, string>> filters, int count, int offset) =>
        $"{baseUrl}/fhir/{resourceType}?" + string.Join('&', filters.Select(f => $"{f.Key}={Uri.EscapeDataString(f.Value)}").Append($"_count={count}").Append($"_offset={offset}"));
}
