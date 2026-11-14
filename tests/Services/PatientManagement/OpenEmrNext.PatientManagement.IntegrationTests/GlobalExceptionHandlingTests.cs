using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Domain.Fhir.ReadModels;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace OpenEmrNext.PatientManagement.IntegrationTests;

/// <summary>
/// Proves an exception no endpoint's own catch block anticipates still gets the same
/// <see cref="ProblemDetails"/> shape every other error path in this API returns, instead of ASP.NET
/// Core's bare default 500 -- the fix for the "no global exception-handling middleware" finding
/// (<c>AddProblemDetails</c> + <c>app.UseExceptionHandler()</c>, <c>Program.cs</c>). Forces the failure
/// by swapping in a repository that always throws, rather than hunting for a real code path that
/// happens to leave something unhandled -- the point is to test the middleware itself.
/// </summary>
[Collection("PatientDataContainer")]
public class GlobalExceptionHandlingTests(PatientDataContainerFixture fixture) : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    public Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            [
                new("PatientManagementDb:Host", fixture.Hostname),
                new("PatientManagementDb:Port", fixture.Port.ToString()),
                new("PatientManagementDb:Database", fixture.Database),
                new("PatientManagementDb:User", fixture.User),
                new("PatientManagementDb:Password", fixture.DbPassword),
                new("AdvanceDirectiveStorage:RepositoryRoot", Path.GetTempPath())
            ]));

            builder.ConfigureTestServices(services =>
                services.AddScoped<IPatientFhirRepository, AlwaysThrowsPatientFhirRepository>());
        });
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
    public async Task An_unanticipated_exception_returns_problem_details_not_a_bare_500()
    {
        var response = await _client!.GetAsync($"/fhir/Patient/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status500InternalServerError, problem!.Status);
    }

    private sealed class AlwaysThrowsPatientFhirRepository : IPatientFhirRepository
    {
        public Task<PatientFhirView?> GetByUuidAsync(Guid uuid, CancellationToken ct) =>
            throw new InvalidOperationException("Simulated unanticipated failure.");

        public Task<IReadOnlyList<PatientFhirView>> SearchAsync(PatientFhirSearchCriteria criteria, CancellationToken ct) =>
            throw new InvalidOperationException("Simulated unanticipated failure.");
    }
}
