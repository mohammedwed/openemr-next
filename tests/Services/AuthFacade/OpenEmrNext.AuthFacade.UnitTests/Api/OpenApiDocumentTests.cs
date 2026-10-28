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
}
