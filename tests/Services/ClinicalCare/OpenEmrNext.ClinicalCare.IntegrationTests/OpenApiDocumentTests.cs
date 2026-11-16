using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// The generated OpenAPI document (API-9, API-10). A client built from it, including Scalar's "Try it" UI, takes
/// field types and the auth requirement from here, so a wrong or missing one describes a request the real endpoint
/// rejects. Needs no database: the document is generated from the code, so placeholder configuration is enough.
/// </summary>
public class OpenApiDocumentTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public OpenApiDocumentTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            [
                new("ClinicalCareDb:Host", "openapi-test-placeholder"),
                new("ClinicalCareDb:Database", "openapi-test-placeholder"),
                new("ClinicalCareDb:User", "openapi-test-placeholder"),
                new("ClinicalCareDb:Password", "openapi-test-placeholder"),
                new("InternalGateway:Secret", "test-secret"),
                new("InternalEvents:Secret", "test-events-secret")
            ]));
        });
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task<JsonDocument> ReadDocumentAsync() =>
        JsonDocument.Parse(await _client.GetStringAsync("/openapi/v1.json"));

    [Fact]
    public async Task Document_describes_the_api()
    {
        using var document = await ReadDocumentAsync();

        var info = document.RootElement.GetProperty("info");

        Assert.Equal("ClinicalCare API", info.GetProperty("title").GetString());
        Assert.False(string.IsNullOrWhiteSpace(info.GetProperty("description").GetString()));
    }

    [Fact]
    public async Task Document_declares_the_gateway_secret_scheme()
    {
        using var document = await ReadDocumentAsync();

        var scheme = document.RootElement.GetProperty("components").GetProperty("securitySchemes")
            .GetProperty("InternalGateway");

        Assert.Equal("apiKey", scheme.GetProperty("type").GetString());
        Assert.Equal("header", scheme.GetProperty("in").GetString());
        Assert.Equal("X-Internal-Gateway-Secret", scheme.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Every_operation_requires_the_gateway_secret()
    {
        using var document = await ReadDocumentAsync();

        foreach (var pathEntry in document.RootElement.GetProperty("paths").EnumerateObject())
        {
            foreach (var operationEntry in pathEntry.Value.EnumerateObject())
            {
                Assert.True(operationEntry.Value.TryGetProperty("security", out var security),
                    $"{operationEntry.Name} {pathEntry.Name} has no security requirement.");
                var requirement = Assert.Single(security.EnumerateArray());
                Assert.Contains("InternalGateway", requirement.EnumerateObject().Select(p => p.Name));
            }
        }
    }

    [Fact]
    public async Task CreateEncounterRequest_schema_reflects_its_validation_attributes()
    {
        using var document = await ReadDocumentAsync();

        var schema = document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("CreateEncounterRequest").GetProperty("properties");

        Assert.Equal(1, schema.GetProperty("calendarCategoryId").GetProperty("minimum").GetInt32());
        Assert.Equal(65_535, schema.GetProperty("reason").GetProperty("maxLength").GetInt32());
        Assert.Equal(30, schema.GetProperty("sensitivity").GetProperty("maxLength").GetInt32());
        Assert.Equal(1, schema.GetProperty("posCode").GetProperty("minimum").GetInt32());
        Assert.Equal(99, schema.GetProperty("posCode").GetProperty("maximum").GetInt32());

        // providerId is bounded by the int column legacy keeps it in (review finding R-1), so the document states a real maximum.
        var providerId = schema.GetProperty("providerId");
        Assert.Equal(1, providerId.GetProperty("minimum").GetInt32());
        Assert.Equal(int.MaxValue, providerId.GetProperty("maximum").GetInt64());
    }

    [Fact]
    public async Task The_committed_snapshot_matches_the_live_document_so_the_client_cannot_silently_drift()
    {
        // The typed client is generated from the committed snapshot, not the live API. If the API changes and the
        // snapshot is not regenerated, a client keeps compiling against a contract the server no longer honours.
        // Fix a failure here with: bash scripts/dev/generate-clinicalcare-openapi.sh
        var live = JsonNode.Parse(await _client.GetStringAsync("/openapi/v1.json"))!.AsObject();
        live.Remove("servers"); // the generator drops it too: it is just that run's loopback port
        var committed = JsonNode.Parse(await File.ReadAllTextAsync(RepoFiles.Find(
            "src/Services/ClinicalCare/OpenEmrNext.ClinicalCare.Client/OpenAPIs/ClinicalCare.json")))!;

        Assert.True(JsonNode.DeepEquals(committed, live),
            "OpenAPIs/ClinicalCare.json is out of date. Run: bash scripts/dev/generate-clinicalcare-openapi.sh");
    }
}
