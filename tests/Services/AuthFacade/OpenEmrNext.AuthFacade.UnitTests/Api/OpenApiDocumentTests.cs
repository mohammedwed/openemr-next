using System.Text.Json;

namespace OpenEmrNext.AuthFacade.UnitTests.Api;

/// <summary>
/// Checks the generated OpenAPI document. Clients built from it, including the Scalar UI, take the field types
/// from here, so a form field described as an object makes them send something the endpoint does not parse.
/// </summary>
public class OpenApiDocumentTests
{
    private static async Task<JsonDocument> ReadDocumentAsync()
    {
        await using var api = await TestApi.StartAsync();
        return JsonDocument.Parse(await api.Client.GetStringAsync("/openapi/v1.json"));
    }

    private static JsonElement Operation(JsonDocument document, string path, string method) =>
        document.RootElement.GetProperty("paths").GetProperty(path).GetProperty(method);

    private static List<string> ResponseCodes(JsonElement operation) =>
        operation.GetProperty("responses").EnumerateObject().Select(r => r.Name).Order().ToList();

    [Fact]
    public async Task Document_describes_the_api()
    {
        using var document = await ReadDocumentAsync();

        var info = document.RootElement.GetProperty("info");

        Assert.Equal("AuthFacade API", info.GetProperty("title").GetString());
        Assert.False(string.IsNullOrWhiteSpace(info.GetProperty("description").GetString()));
    }

    [Fact]
    public async Task Document_declares_the_session_cookie_scheme()
    {
        using var document = await ReadDocumentAsync();

        var scheme = document.RootElement.GetProperty("components").GetProperty("securitySchemes")
            .GetProperty(".AspNetCore.AuthFacadeSession");

        Assert.Equal("apiKey", scheme.GetProperty("type").GetString());
        Assert.Equal("cookie", scheme.GetProperty("in").GetString());
        Assert.Equal(".AspNetCore.AuthFacadeSession", scheme.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Only_the_current_user_operation_requires_authorization()
    {
        using var document = await ReadDocumentAsync();

        var me = Operation(document, "/api/v1/me", "get");

        Assert.True(me.TryGetProperty("security", out var security));
        var requirement = Assert.Single(security.EnumerateArray());
        var schemeNames = requirement.EnumerateObject().Select(p => p.Name).ToList();
        Assert.Contains(".AspNetCore.AuthFacadeSession", schemeNames);
    }

    [Fact]
    public async Task Current_user_operation_documents_its_responses()
    {
        using var document = await ReadDocumentAsync();

        var codes = ResponseCodes(Operation(document, "/api/v1/me", "get"));

        Assert.Equal(["200", "401"], codes);
    }

    [Theory]
    [InlineData("MeResponse")]
    public async Task Response_bodies_are_in_the_document(string schemaName)
    {
        using var document = await ReadDocumentAsync();

        Assert.True(document.RootElement.GetProperty("components").GetProperty("schemas").TryGetProperty(schemaName, out _));
    }

    [Theory]
    [InlineData("/api/v1/me", "get", "Identity")]
    public async Task Operations_have_a_summary_and_their_own_tag(string path, string method, string tag)
    {
        using var document = await ReadDocumentAsync();

        var operation = Operation(document, path, method);

        Assert.False(string.IsNullOrWhiteSpace(operation.GetProperty("summary").GetString()));
        Assert.Equal([tag], operation.GetProperty("tags").EnumerateArray().Select(t => t.GetString()).ToList());
    }
}
