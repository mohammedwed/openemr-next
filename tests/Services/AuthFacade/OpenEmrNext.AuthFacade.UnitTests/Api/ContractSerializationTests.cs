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
