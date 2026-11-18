using System.Text.Json;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// The gateway forwards only the path prefixes it is told to (ADR 0064): a Clinical Care route group the gateway does not list is reachable against the service but 404s for every browser. Immunizations and the
/// encounter lookups were built without their route, which nothing caught because the gateway's own tests run only against a live stack. This pins that every prefix the Clinical Care API serves is routed.
/// </summary>
public class GatewayRoutesTests
{
    [Fact]
    public async Task Every_api_prefix_the_clinical_care_api_serves_is_routed_to_it_by_the_gateway()
    {
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(RepoFiles.Find("src/Services/ClinicalCare/OpenEmrNext.ClinicalCare.Client/OpenAPIs/ClinicalCare.json")));
        var served = document.RootElement.GetProperty("paths").EnumerateObject()
            .Select(p => string.Join('/', p.Name.Split('/').Take(p.Name.StartsWith("/fhir/", StringComparison.Ordinal) ? 3 : 4)))   // "/api/v1/immunizations", "/fhir/Immunization"
            .Distinct().Order().ToList();
        var gateway = await File.ReadAllTextAsync(RepoFiles.Find("src/Services/Gateway/OpenEmrNext.Gateway/Program.cs"));

        Assert.NotEmpty(served);
        foreach (var prefix in served)
            Assert.True(gateway.Contains($"\"{prefix}/{{**catch-all}}\"", StringComparison.Ordinal), $"The gateway has no route for {prefix}.");
    }
}
