using System.Net;

namespace OpenEmrNext.AuthFacade.UnitTests.Api;

/// <summary>Tests the unauthenticated system surface (<c>/health</c>) in-process through <see cref="TestApi"/>.</summary>
public class ApiEndpointTests
{
    [Fact]
    public async Task Hello_is_anonymous()
    {
        await using var api = await TestApi.StartAsync();

        using var response = await api.Client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Health_is_no_longer_served_under_api()
    {
        await using var api = await TestApi.StartAsync();

        using var response = await api.Client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
