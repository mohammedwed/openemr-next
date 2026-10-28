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

    [Theory]
    [InlineData("PasswordGrant")]
    [InlineData("Username")]
    [InlineData("Password")]
    public async Task Form_field_value_objects_are_described_as_strings(string schemaName)
    {
        using var document = await ReadDocumentAsync();

        var schema = document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty(schemaName);

        Assert.Equal("string", schema.GetProperty("type").GetString());
        Assert.False(schema.TryGetProperty("properties", out _));
    }

    [Fact]
    public async Task Password_field_is_marked_as_a_password()
    {
        using var document = await ReadDocumentAsync();

        var schema = document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("Password");

        Assert.Equal("password", schema.GetProperty("format").GetString());
    }

    [Fact]
    public async Task Token_endpoint_requires_the_three_form_fields()
    {
        using var document = await ReadDocumentAsync();

        var body = document.RootElement.GetProperty("paths").GetProperty("/api/v1/connect/token")
            .GetProperty("post").GetProperty("requestBody").GetProperty("content")
            .GetProperty("application/x-www-form-urlencoded").GetProperty("schema");
        var required = body.GetProperty("required").EnumerateArray().Select(e => e.GetString()).ToList();

        Assert.Equal(["grant_type", "username", "password"], required);
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
    public async Task Document_declares_the_bearer_scheme()
    {
        using var document = await ReadDocumentAsync();

        var scheme = document.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");

        Assert.Equal("http", scheme.GetProperty("type").GetString());
        Assert.Equal("bearer", scheme.GetProperty("scheme").GetString());
    }

    [Fact]
    public async Task Only_the_current_user_operation_requires_the_bearer_token()
    {
        using var document = await ReadDocumentAsync();

        var me = Operation(document, "/api/v1/me", "get");
        var token = Operation(document, "/api/v1/connect/token", "post");

        Assert.True(me.TryGetProperty("security", out var security));
        Assert.Contains("Bearer", security.EnumerateArray().SelectMany(r => r.EnumerateObject().Select(p => p.Name)));
        Assert.False(token.TryGetProperty("security", out _));
    }

    [Fact]
    public async Task Token_operation_accepts_only_urlencoded_forms()
    {
        using var document = await ReadDocumentAsync();

        var content = Operation(document, "/api/v1/connect/token", "post").GetProperty("requestBody").GetProperty("content");

        Assert.Equal(["application/x-www-form-urlencoded"], content.EnumerateObject().Select(c => c.Name).ToList());
    }

    [Fact]
    public async Task Token_operation_documents_every_response()
    {
        using var document = await ReadDocumentAsync();

        var codes = ResponseCodes(Operation(document, "/api/v1/connect/token", "post"));

        Assert.Equal(["200", "400", "415", "429", "502", "503"], codes);
    }

    [Fact]
    public async Task Current_user_operation_documents_its_responses()
    {
        using var document = await ReadDocumentAsync();

        var codes = ResponseCodes(Operation(document, "/api/v1/me", "get"));

        Assert.Equal(["200", "401"], codes);
    }

    [Theory]
    [InlineData("TokenResponse")]
    [InlineData("ErrorResponse")]
    [InlineData("MeResponse")]
    public async Task Response_bodies_are_in_the_document(string schemaName)
    {
        using var document = await ReadDocumentAsync();

        Assert.True(document.RootElement.GetProperty("components").GetProperty("schemas").TryGetProperty(schemaName, out _));
    }

    [Theory]
    [InlineData("/api/v1/connect/token", "post", "Authentication")]
    [InlineData("/api/v1/me", "get", "Identity")]
    public async Task Operations_have_a_summary_and_their_own_tag(string path, string method, string tag)
    {
        using var document = await ReadDocumentAsync();

        var operation = Operation(document, path, method);

        Assert.False(string.IsNullOrWhiteSpace(operation.GetProperty("summary").GetString()));
        Assert.Equal([tag], operation.GetProperty("tags").EnumerateArray().Select(t => t.GetString()).ToList());
    }
}
