using System.Text.Json;

namespace OpenEmrNext.Gateway.ProxyTests;

/// <summary>
/// The gateway's session check reads one property of AuthFacade's <c>GET /api/v1/me</c> answer. These tests tie that property name to AuthFacade's own published contract, so the two cannot drift apart again: the first
/// version of the check looked for the JWT claim's name ("sub") instead of the response's ("subject"), the fake AuthFacade in the tests made the same guess, and every real session looked signed out.
/// </summary>
public class AuthFacadeContractTests
{
    private static JsonElement MeResponse()
    {
        const string Relative = "src/Services/AuthFacade/OpenEmrNext.AuthFacade.Client/OpenAPIs/AuthFacade.json";
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, Relative)))
            directory = directory.Parent;

        Assert.NotNull(directory);   // the repository root was found from the test binary
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory.FullName, Relative)));
        return document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("MeResponse").Clone();
    }

    [Fact]
    public void The_property_the_gateway_reads_is_one_AuthFacades_me_response_actually_has()
    {
        var properties = MeResponse().GetProperty("properties").EnumerateObject().Select(p => p.Name).ToList();

        Assert.Contains(SessionVerifier.SubjectProperty, properties);
    }

    [Fact]
    public void The_jwt_claim_name_is_not_a_property_of_the_response_so_it_must_not_be_what_the_gateway_reads()
    {
        var properties = MeResponse().GetProperty("properties").EnumerateObject().Select(p => p.Name).ToList();

        Assert.DoesNotContain("sub", properties);
        Assert.NotEqual("sub", SessionVerifier.SubjectProperty);
    }
}
