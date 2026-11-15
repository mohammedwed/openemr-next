using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using OpenEmrNext.PatientManagement.Api.Contracts.Requests;
using Xunit;

namespace OpenEmrNext.PatientManagement.IntegrationTests;

/// <summary>
/// Proves the <c>X-Internal-Gateway-Secret</c> gate (<see cref="OpenEmrNext.PatientManagement.Api.Authentication.InternalGatewayAuthenticationHandler"/>)
/// actually rejects a caller that doesn't present the configured secret, using <c>POST
/// /api/v1/patients</c> as the one representative endpoint. <see cref="PatientEndpointTests"/> already
/// proves the correct-secret path succeeds (its fixture attaches it to every request), so this class
/// only needs to cover the two ways presenting it can fail.
/// </summary>
[Collection("PatientDataContainer")]
public class GatewaySecretAuthenticationTests(PatientDataContainerFixture fixture) : IAsyncLifetime
{
    private const string ConfiguredSecret = "test-secret";
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    public Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            [
                new("PatientManagementDb:Host", fixture.Hostname),
                new("PatientManagementDb:Port", fixture.Port.ToString()),
                new("PatientManagementDb:Database", fixture.Database),
                new("PatientManagementDb:User", fixture.User),
                new("PatientManagementDb:Password", fixture.DbPassword),
                new("AdvanceDirectiveStorage:RepositoryRoot", Path.GetTempPath()),
                new("InternalGateway:Secret", ConfiguredSecret)
            ])));
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        return Task.CompletedTask;
    }

    private static RegisterPatientRequest ValidRequest() =>
        new("Katherine", "Johnson", "Female", new DateOnly(1918, 8, 26), null, null, RegisteredBy: 1);

    [Fact]
    public async Task POST_patients_with_no_gateway_secret_header_returns_401()
    {
        var response = await _client!.PostAsJsonAsync("/api/v1/patients", ValidRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_patients_with_the_wrong_gateway_secret_returns_401()
    {
        _client!.DefaultRequestHeaders.Add("X-Internal-Gateway-Secret", "not-the-configured-secret");

        var response = await _client.PostAsJsonAsync("/api/v1/patients", ValidRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
