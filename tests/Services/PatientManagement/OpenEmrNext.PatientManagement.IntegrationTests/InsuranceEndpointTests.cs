using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using OpenEmrNext.PatientManagement.Api.Contracts.Requests;
using OpenEmrNext.PatientManagement.Api.Contracts.Responses;
using Xunit;

namespace OpenEmrNext.PatientManagement.IntegrationTests;

/// <summary>Exercises the real HTTP pipeline for the insurance endpoints, the same way <see cref="PatientEndpointTests"/> does for registration.</summary>
[Collection("PatientDataContainer")]
public class InsuranceEndpointTests(PatientDataContainerFixture fixture) : IAsyncLifetime
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
                new("PatientManagementDb:Password", fixture.DbPassword),
                new("AdvanceDirectiveStorage:RepositoryRoot", Path.GetTempPath())
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

    private static long NextPid() => Random.Shared.NextInt64(1, long.MaxValue);

    [Fact]
    public async Task POST_insurance_with_a_valid_body_returns_201()
    {
        var pid = NextPid();
        var request = new AddInsurancePolicyRequest("primary", 7, "POL1", null, null, new DateOnly(2026, 1, 1), null);

        var response = await _client!.PostAsJsonAsync($"/api/v1/patients/{pid}/insurance", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<InsurancePolicyAddedResponse>();
        Assert.NotNull(body);
        Assert.Equal("primary", body!.Level);
    }

    [Fact]
    public async Task POST_insurance_with_an_invalid_level_returns_400()
    {
        var pid = NextPid();
        var request = new AddInsurancePolicyRequest("not-a-level", 7, "POL1", null, null, new DateOnly(2026, 1, 1), null);

        var response = await _client!.PostAsJsonAsync($"/api/v1/patients/{pid}/insurance", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GET_effective_returns_the_policy_just_added()
    {
        var pid = NextPid();
        await _client!.PostAsJsonAsync($"/api/v1/patients/{pid}/insurance",
            new AddInsurancePolicyRequest("primary", 7, "POL1", null, null, new DateOnly(2020, 1, 1), null));

        var response = await _client.GetAsync($"/api/v1/patients/{pid}/insurance/effective");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<EffectiveInsuranceResponse>>();
        Assert.NotNull(body);
        var policy = Assert.Single(body!);
        Assert.Equal("primary", policy.Level);
        Assert.Equal(7, policy.CompanyId);
    }

    [Fact]
    public async Task POST_swap_moves_the_policy_to_the_target_level()
    {
        var pid = NextPid();
        var added = await _client!.PostAsJsonAsync($"/api/v1/patients/{pid}/insurance",
            new AddInsurancePolicyRequest("primary", 7, "POL1", null, null, new DateOnly(2020, 1, 1), null));
        var policy = await added.Content.ReadFromJsonAsync<InsurancePolicyAddedResponse>();

        var swapResponse = await _client.PostAsJsonAsync(
            $"/api/v1/patients/{pid}/insurance/swap", new SwapInsuranceRequest("secondary", policy!.Uuid));

        Assert.Equal(HttpStatusCode.NoContent, swapResponse.StatusCode);

        var effective = await (await _client.GetAsync($"/api/v1/patients/{pid}/insurance/effective"))
            .Content.ReadFromJsonAsync<List<EffectiveInsuranceResponse>>();
        Assert.Equal("secondary", Assert.Single(effective!).Level);
    }

    [Fact]
    public async Task POST_swap_with_an_unknown_policy_uuid_returns_404()
    {
        var pid = NextPid();

        var response = await _client!.PostAsJsonAsync(
            $"/api/v1/patients/{pid}/insurance/swap", new SwapInsuranceRequest("secondary", Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
