using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using OpenEmrNext.AuthFacade.Api;
using OpenEmrNext.AuthFacade.Api.Endpoints;

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
        // The dependencies are registered so the handlers' service parameters bind from services.
        // Nothing starts the host, so the required OpenEmr settings are never validated.
        var builder = WebApplication.CreateBuilder();
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
