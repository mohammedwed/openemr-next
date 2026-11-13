using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using OpenEmrNext.PatientManagement.Api.Contracts.Fhir;
using OpenEmrNext.PatientManagement.Api.Contracts.Requests;
using OpenEmrNext.PatientManagement.Api.Contracts.Responses;
using Xunit;

namespace OpenEmrNext.PatientManagement.IntegrationTests;

/// <summary>Exercises the real HTTP pipeline for the FHIR Patient endpoints, the same way
/// <see cref="InsuranceEndpointTests"/> does for insurance.</summary>
[Collection("PatientDataContainer")]
public class FhirPatientEndpointTests(PatientDataContainerFixture fixture) : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private readonly DirectoryInfo _repositoryRoot = Directory.CreateTempSubdirectory("fhir-patient-endpoint-tests-");

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
                new("AdvanceDirectiveStorage:RepositoryRoot", _repositoryRoot.FullName)
            ])));
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        _repositoryRoot.Delete(recursive: true);
        return Task.CompletedTask;
    }

    private async Task<PatientRegisteredResponse> RegisterAsync(string lastName, string gender = "female")
    {
        var response = await _client!.PostAsJsonAsync("/api/v1/patients", new RegisterPatientRequest(
            "Test", lastName, gender, new DateOnly(1995, 3, 3), null, null, 1));
        return (await response.Content.ReadFromJsonAsync<PatientRegisteredResponse>())!;
    }

    [Fact]
    public async Task GET_a_registered_patient_returns_a_fhir_patient_resource()
    {
        var registered = await RegisterAsync($"Fhir{Guid.NewGuid():N}", "male");

        var response = await _client!.GetAsync($"/fhir/Patient/{registered.Uuid}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var patient = await response.Content.ReadFromJsonAsync<FhirPatient>();
        Assert.NotNull(patient);
        Assert.Equal("Patient", patient!.ResourceType);
        Assert.Equal(registered.Uuid.ToString(), patient.Id);
        Assert.Equal("male", patient.Gender);
        Assert.Contains(patient.Extension, e => e.Url.EndsWith("us-core-birthsex") && e.ValueCode == "M");
    }

    [Fact]
    public async Task GET_an_unknown_patient_returns_404()
    {
        var response = await _client!.GetAsync($"/fhir/Patient/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GET_search_by_family_returns_a_bundle_with_the_match()
    {
        var lastName = $"Bundled{Guid.NewGuid():N}";
        var registered = await RegisterAsync(lastName);

        var response = await _client!.GetAsync($"/fhir/Patient?family={lastName}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bundle = await response.Content.ReadFromJsonAsync<FhirBundle>();
        Assert.NotNull(bundle);
        Assert.Equal("Bundle", bundle!.ResourceType);
        Assert.Equal(1, bundle.Total);
        Assert.Equal(registered.Uuid.ToString(), bundle.Entry[0].Resource.Id);
    }

    [Fact]
    public async Task POST_a_fhir_patient_creates_a_real_patient_and_returns_201()
    {
        var request = new CreateFhirPatientRequest(
            Name: [new FhirHumanName("official", $"Created{Guid.NewGuid():N}", ["Fhir"], [], [])],
            Telecom: [new FhirContactPoint("email", "fhir-create@example.com", "home")],
            Gender: "female",
            BirthDate: "1988-08-08");

        var response = await _client!.PostAsJsonAsync("/fhir/Patient?registeredBy=1", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var patient = await response.Content.ReadFromJsonAsync<FhirPatient>();
        Assert.NotNull(patient);
        Assert.Equal("female", patient!.Gender);
        Assert.Equal("Fhir", patient.Name[0].Given[0]);
        Assert.Equal("1988-08-08", patient.BirthDate);
    }

    [Fact]
    public async Task POST_a_fhir_patient_with_no_last_name_returns_400()
    {
        var request = new CreateFhirPatientRequest(
            Name: [new FhirHumanName("official", "", ["NoLastName"], [], [])],
            Telecom: [],
            Gender: "unknown",
            BirthDate: "1988-08-08");

        var response = await _client!.PostAsJsonAsync("/fhir/Patient?registeredBy=1", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
