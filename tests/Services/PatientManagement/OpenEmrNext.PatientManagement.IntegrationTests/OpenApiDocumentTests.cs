using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace OpenEmrNext.PatientManagement.IntegrationTests;

/// <summary>
/// Checks the generated OpenAPI document -- same reasoning as AuthFacade's own
/// <c>OpenApiDocumentTests</c>: a client built from it, including Scalar's "Try it" UI, takes field
/// types and the auth requirement from here, so a wrong or missing one sends a request the real
/// endpoint rejects or mis-describes what it actually needs.
/// </summary>
[Collection("PatientDataContainer")]
public class OpenApiDocumentTests(PatientDataContainerFixture fixture) : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    public Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            [
                new("PatientManagementDb:Host", fixture.Hostname),
                new("PatientManagementDb:Port", fixture.Port.ToString()),
                new("PatientManagementDb:Database", fixture.Database),
                new("PatientManagementDb:User", fixture.User),
                new("PatientManagementDb:Password", fixture.DbPassword),
                new("AdvanceDirectiveStorage:RepositoryRoot", Path.GetTempPath()),
                new("InternalGateway:Secret", "test-secret")
            ]));
        });
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        return Task.CompletedTask;
    }

    private async Task<JsonDocument> ReadDocumentAsync() =>
        JsonDocument.Parse(await _client!.GetStringAsync("/openapi/v1.json"));

    [Fact]
    public async Task Document_describes_the_api()
    {
        using var document = await ReadDocumentAsync();

        var info = document.RootElement.GetProperty("info");

        Assert.Equal("PatientManagement API", info.GetProperty("title").GetString());
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
    public async Task RegisterPatientRequest_schema_reflects_its_validation_attributes()
    {
        using var document = await ReadDocumentAsync();

        var schema = document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("RegisterPatientRequest").GetProperty("properties");

        Assert.Equal(1, schema.GetProperty("firstName").GetProperty("minLength").GetInt32());
        Assert.Equal(255, schema.GetProperty("firstName").GetProperty("maxLength").GetInt32());
        Assert.Equal("email", schema.GetProperty("email").GetProperty("format").GetString());

        // Every [Range(1, long.MaxValue)] in these DTOs is a "must be positive" idiom, not a real
        // upper bound -- the real long.MaxValue is a lossy double by the time RangeAttribute itself
        // sees it, so the transformer deliberately omits a wrong "maximum" rather than writing one.
        var registeredBy = schema.GetProperty("registeredBy");
        Assert.Equal(1, registeredBy.GetProperty("minimum").GetInt32());
        Assert.False(registeredBy.TryGetProperty("maximum", out _));
    }
}
