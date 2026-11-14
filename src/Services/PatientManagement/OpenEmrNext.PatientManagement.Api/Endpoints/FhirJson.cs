using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace OpenEmrNext.PatientManagement.Api.Endpoints;

/// <summary>
/// FHIR JSON serialization for the <c>/fhir</c> endpoints. Hl7.Fhir.R4 5.11.1 (pinned for the
/// validator, see <c>docs/m4-domain-notes.md</c>'s FHIR SDK section) has no native
/// <c>System.Text.Json</c> converter the way the current 6.x line does -- <see cref="FhirJsonSerializer"/>
/// and <see cref="FhirJsonParser"/> serialize to/from a plain string instead, so this service's FHIR
/// endpoints read/write that string directly rather than relying on ASP.NET Core's implicit JSON
/// body binding.
/// </summary>
public static class FhirJson
{
    private static readonly FhirJsonSerializer Serializer = new();
    private static readonly FhirJsonParser Parser = new();

    public static string Serialize(Resource resource) => Serializer.SerializeToString(resource);

    public static T Parse<T>(string json) where T : Resource => Parser.Parse<T>(json);
}
