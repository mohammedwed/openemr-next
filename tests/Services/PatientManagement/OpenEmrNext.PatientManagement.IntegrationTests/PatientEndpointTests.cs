using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using OpenEmrNext.PatientManagement.Api.Contracts.Requests;
using OpenEmrNext.PatientManagement.Api.Contracts.Responses;
using Xunit;

namespace OpenEmrNext.PatientManagement.IntegrationTests;

/// <summary>
/// Exercises the real HTTP pipeline -- routing, model binding, JSON, status codes -- in-process via
/// <see cref="WebApplicationFactory{TEntryPoint}"/>, against the same Testcontainers database the
/// lower-level <see cref="PatientRegistrationTests"/> use. <see cref="PatientRegistrationTests"/>
/// already proves the persistence/outbox behavior directly against the <c>DbContext</c>; this class
/// proves the endpoint built on top of it actually works end to end, not just the pieces underneath it.
/// </summary>
[Collection("PatientDataContainer")]
public class PatientEndpointTests(PatientDataContainerFixture fixture) : IAsyncLifetime
{
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
                new("PatientManagementDb:Password", fixture.DbPassword)
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

    [Fact]
    public async Task POST_patients_with_valid_body_returns_201_with_the_patients_uuid()
    {
        var request = new RegisterPatientRequest("Katherine", "Johnson", "Female", new DateOnly(1918, 8, 26), null, null, RegisteredBy: 1);

        var response = await _client!.PostAsJsonAsync("/api/v1/patients", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PatientRegisteredResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.Uuid);
        Assert.NotEqual(0, body.Pid);
        Assert.Equal(body.Pid.ToString(), body.Pubpid);
        Assert.Equal($"/api/v1/patients/{body.Uuid}", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task POST_patients_with_an_invalid_body_returns_400_with_the_validation_errors()
    {
        var request = new RegisterPatientRequest("Katherine", "J", "F", new DateOnly(1918, 8, 26), null, null, RegisteredBy: 1);

        var response = await _client!.PostAsJsonAsync("/api/v1/patients", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(problem);
        Assert.True(problem!.ContainsKey("errors"));
    }
}
