using Hl7.Fhir.Model;

namespace OpenEmrNext.ClinicalCare.Api.Fhir;

/// <summary>Times: the service stores local wall-clock times (as the JSON API reads them) and FHIR needs an offset on any date-time that has a time, so the server's own offset is given (ADR 0079, limitation of every resource).</summary>
internal static class FhirTime
{
    public static FhirDateTime DateTime(System.DateTime local) =>
        new(new DateTimeOffset(System.DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeZoneInfo.Local.GetUtcOffset(local)));

    public static DateTimeOffset Instant(System.DateTime local) =>
        new(System.DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeZoneInfo.Local.GetUtcOffset(local));
}

/// <summary>
/// Turns legacy's diagnosis text, <c>SYSTEM:code;SYSTEM:code</c>, into FHIR codings. The systems legacy's code lists use are mapped to their FHIR code-system URIs; one it does not know becomes a
/// <c>urn:openemr:code-system:{name}</c> URI, so the code is still carried and still names where it came from.
/// </summary>
internal static class DiagnosisCodings
{
    private static readonly Dictionary<string, string> Systems = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ICD10"] = "http://hl7.org/fhir/sid/icd-10-cm",
        ["ICD9"] = "http://hl7.org/fhir/sid/icd-9-cm",
        ["SNOMED"] = "http://snomed.info/sct",
        ["SNOMED-CT"] = "http://snomed.info/sct",
        ["RXCUI"] = "http://www.nlm.nih.gov/research/umls/rxnorm",
        ["LOINC"] = "http://loinc.org",
        ["CVX"] = "http://hl7.org/fhir/sid/cvx",
        ["CPT4"] = "http://www.ama-assn.org/go/cpt"
    };

    public static List<Coding> Parse(string? diagnosis)
    {
        var codings = new List<Coding>();
        foreach (var part in (diagnosis ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colon = part.IndexOf(':');
            if (colon <= 0 || colon == part.Length - 1)
                continue;   // not SYSTEM:code: nothing to say about it that is not a guess

            var system = part[..colon].Trim();
            codings.Add(new Coding(Systems.TryGetValue(system, out var uri) ? uri : $"urn:openemr:code-system:{system}", part[(colon + 1)..].Trim()));
        }

        return codings;
    }

    /// <summary>The concept for a problem or an allergy: its codes when it has any, and always its title as the text a person reads.</summary>
    public static CodeableConcept Concept(string? diagnosis, string? title)
    {
        var concept = new CodeableConcept { Coding = Parse(diagnosis) };
        if (!string.IsNullOrWhiteSpace(title))
            concept.Text = title.Trim();
        return concept;
    }
}
