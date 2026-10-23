using System.Text.Json;
using OpenEmrNext.AuthFacade.Api.Contracts.Responses;

namespace OpenEmrNext.AuthFacade.UnitTests.Api;

/// <summary>
/// Pins the JSON shape of each contract type. The options match ASP.NET Core's minimal API
/// defaults (<see cref="JsonSerializerDefaults.Web"/>), so these tests catch a change that would
/// alter what clients receive.
/// </summary>
public class ContractSerializationTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>Serializes a value and parses the result, so tests can check individual properties.</summary>
    private static JsonElement Serialize<T>(T value)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(value, Options));
        return doc.RootElement.Clone();
    }

    [Fact]
    public void Error_response_omits_error_description_when_it_is_null()
    {
        var json = Serialize(new ErrorResponse("invalid_request"));

        Assert.Equal("invalid_request", json.GetProperty("error").GetString());
        Assert.False(json.TryGetProperty("error_description", out _));
    }

    [Fact]
    public void Error_response_includes_error_description_when_it_is_set()
    {
        var json = Serialize(new ErrorResponse("server_error", "OpenEMR is unreachable."));

        Assert.Equal("OpenEMR is unreachable.", json.GetProperty("error_description").GetString());
    }

    [Fact]
    public void Token_response_uses_snake_case_names()
    {
        var json = Serialize(new TokenResponse("openemr-token", "Bearer", 3600, "r-1"));

        Assert.Equal("openemr-token", json.GetProperty("access_token").GetString());
        Assert.Equal("Bearer", json.GetProperty("token_type").GetString());
        Assert.Equal(3600, json.GetProperty("expires_in").GetInt32());
        Assert.Equal("r-1", json.GetProperty("refresh_token").GetString());
    }

    [Fact]
    public void Token_response_omits_expires_in_and_refresh_token_when_null()
    {
        var json = Serialize(new TokenResponse("openemr-token", "Bearer", null, null));

        Assert.False(json.TryGetProperty("expires_in", out _));
        Assert.False(json.TryGetProperty("refresh_token", out _));
    }

    [Fact]
    public void Me_response_uses_camel_case_names()
    {
        var json = Serialize(new MeResponse("subject-1", "alice_01"));

        Assert.Equal("subject-1", json.GetProperty("subject").GetString());
        Assert.Equal("alice_01", json.GetProperty("name").GetString());
    }

    [Fact]
    public void Hello_response_uses_the_message_name()
    {
        var json = Serialize(new HelloResponse("Hello from AuthFacade"));

        Assert.Equal("Hello from AuthFacade", json.GetProperty("message").GetString());
    }
}
