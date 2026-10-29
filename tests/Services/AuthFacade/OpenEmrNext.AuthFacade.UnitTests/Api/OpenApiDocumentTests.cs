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

    [Fact]
    public async Task Token_form_fields_are_described_as_strings_with_their_limits()
    {
        using var document = await ReadDocumentAsync();

        var properties = FormSchema(document).GetProperty("properties");

        Assert.Equal("string", properties.GetProperty("grant_type").GetProperty("type").GetString());
        Assert.Equal("string", properties.GetProperty("username").GetProperty("type").GetString());
        Assert.Equal(255, properties.GetProperty("username").GetProperty("maxLength").GetInt32());
        Assert.Equal("string", properties.GetProperty("password").GetProperty("type").GetString());
        Assert.Equal(1024, properties.GetProperty("password").GetProperty("maxLength").GetInt32());
        Assert.Equal("string", properties.GetProperty("refresh_token").GetProperty("type").GetString());
        Assert.Equal(4096, properties.GetProperty("refresh_token").GetProperty("maxLength").GetInt32());
    }

    [Fact]
    public async Task Password_and_refresh_token_fields_are_marked_as_passwords()
    {
        using var document = await ReadDocumentAsync();

        var properties = FormSchema(document).GetProperty("properties");

        Assert.Equal("password", properties.GetProperty("password").GetProperty("format").GetString());
        Assert.Equal("password", properties.GetProperty("refresh_token").GetProperty("format").GetString());
    }

    [Fact]
    public async Task Token_endpoint_requires_only_the_grant_type()
    {
        using var document = await ReadDocumentAsync();

        var required = FormSchema(document).GetProperty("required").EnumerateArray().Select(e => e.GetString()).ToList();

        Assert.Equal(["grant_type"], required);
    }

    /// <summary>The schema of the token endpoint's form body, which must be one object rather than a list of parts.</summary>
    private static JsonElement FormSchema(JsonDocument document)
    {
        var schema = document.RootElement.GetProperty("paths").GetProperty("/api/v1/connect/token")
            .GetProperty("post").GetProperty("requestBody").GetProperty("content")
            .GetProperty("application/x-www-form-urlencoded").GetProperty("schema");
        Assert.Equal("object", schema.GetProperty("type").GetString());
        Assert.False(schema.TryGetProperty("allOf", out _));
        return schema;
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
