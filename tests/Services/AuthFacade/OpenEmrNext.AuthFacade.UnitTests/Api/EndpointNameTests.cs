using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using OpenEmrNext.AuthFacade.Api;

namespace OpenEmrNext.AuthFacade.UnitTests.Api;

/// <summary>
/// Checks that the documented endpoints keep their names. A name is the OpenAPI <c>operationId</c>, so renaming
/// it changes the method name in every generated client.
/// </summary>
public class EndpointNameTests
{
    /// <summary>Builds the app's endpoints and returns the endpoint names they carry.</summary>
    private static List<string> EndpointNames()
    {
        // Nothing starts the host, so OpenEmrOptions.ValidateOnStart() never runs — but the OpenIddict
        // client registration (OpenIddictClientServiceCollectionExtensions.AddOpenIddictClient) builds a
        // Uri from OpenEmr:Issuer eagerly, at registration time, so these minimal values still have to be
        // present and well-formed even though nothing here checks them for correctness.
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenEmr:BaseUrl"] = "https://openemr.test",
            ["OpenEmr:Issuer"] = "https://openemr.test/oauth2/default",
            ["OpenEmr:ClientId"] = "facade-client",
            ["OpenEmr:ClientSecret"] = "facade-secret",
            ["OpenEmr:RedirectUri"] = "https://facade.test/callback",
            ["BrowserFlow:DefaultReturnUrl"] = "https://app.test/",
            ["BrowserFlow:ReturnUrlAllowedHosts:0"] = "app.test"
        });
        builder.AddAuthFacadeHttpDependencies();
        var app = builder.Build();
        app.MapAuthFacadeEndpoints();

        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .Select(endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName)
            .OfType<string>()
            .ToList();
    }

    [Theory]
    [InlineData("IssueToken")]
    [InlineData("GetCurrentUser")]
    public void Public_endpoints_keep_their_names(string name)
    {
        Assert.Contains(name, EndpointNames());
    }
}
