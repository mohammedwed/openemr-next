using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenEmrNext.ClinicalCare.Api.Contracts.Requests;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// QLT-12: when something unanticipated fails after validation, the caller still gets the standard
/// problem+json 500 (OPS-8, ADR 0057 pattern), not a bare default, and nothing is half-written.
/// </summary>
[Collection("ClinicalCareContainer")]
public class GlobalExceptionHandlingTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    private sealed class AlwaysThrowingRegistry : IEncounterRegistry
    {
        public Task RegisterAsync(Encounter encounter, EncounterCreatedEvent @event, CancellationToken ct) =>
            throw new InvalidOperationException("simulated storage failure");
    }

    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await fixture.SeedPatientAsync(EncounterTestData.PatientUuid, EncounterTestData.PatientPid);
        _factory = ApiFactory.Create(fixture, builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEncounterRegistry>();
                services.AddScoped<IEncounterRegistry, AlwaysThrowingRegistry>();
            }));
        _client = _factory.ClientWithSecret();
    }

    public Task DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task An_unexpected_failure_returns_a_problem_json_500_without_leaking_the_exception()
    {
        var request = new CreateEncounterRequest(
            EncounterTestData.PatientUuid, null, 5, null, "Cough", 3, null, 7, null, null);

        var response = await _client!.PostAsJsonAsync("/api/v1/encounters", request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("simulated storage failure", await response.Content.ReadAsStringAsync());
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM form_encounter"));
    }
}
