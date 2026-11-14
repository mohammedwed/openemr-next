using System.Net;
using System.Net.Http.Json;
using System.Text;
using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using OpenEmrNext.PatientManagement.Api.Contracts.Requests;
using OpenEmrNext.PatientManagement.Api.Contracts.Responses;
using OpenEmrNext.PatientManagement.Api.Endpoints;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace OpenEmrNext.PatientManagement.IntegrationTests;

/// <summary>Exercises the real HTTP pipeline for the FHIR Patient endpoints -- real FHIR R4 JSON in
/// and out, through Firely's generated <see cref="Patient"/> model and <see cref="FhirJson"/>, the
/// same way <see cref="InsuranceEndpointTests"/> does for insurance's own contracts.</summary>
[Collection("PatientDataContainer")]
public class FhirPatientEndpointTests(PatientDataContainerFixture fixture) : IAsyncLifetime
{
    private const string FhirJsonContentType = "application/fhir+json";
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

    private static StringContent FhirBody(Resource resource) =>
        new(FhirJson.Serialize(resource), Encoding.UTF8, FhirJsonContentType);

    [Fact]
    public async Task GET_a_registered_patient_returns_a_fhir_patient_resource()
    {
        var registered = await RegisterAsync($"Fhir{Guid.NewGuid():N}", "male");

        var response = await _client!.GetAsync($"/fhir/Patient/{registered.Uuid}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(FhirJsonContentType, response.Content.Headers.ContentType?.MediaType);
        var patient = FhirJson.Parse<Patient>(await response.Content.ReadAsStringAsync());
        Assert.Equal(registered.Uuid.ToString(), patient.Id);
        Assert.Equal(AdministrativeGender.Male, patient.Gender);
        Assert.Contains(patient.Extension, e => e.Url.EndsWith("us-core-birthsex") && (e.Value as Code)?.Value == "M");
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
        var bundle = FhirJson.Parse<Bundle>(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, bundle.Total);
        Assert.Equal(registered.Uuid.ToString(), bundle.Entry[0].Resource.Id);
    }

    [Fact]
    public async Task POST_a_fhir_patient_creates_a_real_patient_and_returns_201()
    {
        var request = new Patient();
        request.Name.Add(new HumanName { Use = HumanName.NameUse.Official, Family = $"Created{Guid.NewGuid():N}", Given = ["Fhir"] });
        request.Telecom.Add(new ContactPoint(ContactPoint.ContactPointSystem.Email, ContactPoint.ContactPointUse.Home, "fhir-create@example.com"));
        request.Gender = AdministrativeGender.Female;
        request.BirthDate = "1988-08-08";

        var response = await _client!.PostAsync("/fhir/Patient?registeredBy=1", FhirBody(request));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var patient = FhirJson.Parse<Patient>(await response.Content.ReadAsStringAsync());
        Assert.Equal(AdministrativeGender.Female, patient.Gender);
        Assert.Equal("Fhir", patient.Name[0].Given.First());
        Assert.Equal("1988-08-08", patient.BirthDate);
    }

    [Fact]
    public async Task POST_a_fhir_patient_with_no_last_name_returns_400()
    {
        var request = new Patient();
        request.Name.Add(new HumanName { Use = HumanName.NameUse.Official, Given = ["NoLastName"] });
        request.Gender = AdministrativeGender.Unknown;
        request.BirthDate = "1988-08-08";

        var response = await _client!.PostAsync("/fhir/Patient?registeredBy=1", FhirBody(request));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_validate_rejects_a_patient_missing_a_required_identifier()
    {
        var request = new Patient { Active = true };
        request.Name.Add(new HumanName { Use = HumanName.NameUse.Official, Family = "Doe", Given = ["Jane"] });
        request.Gender = AdministrativeGender.Female;

        var response = await _client!.PostAsync("/fhir/Patient/$validate", FhirBody(request));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var outcome = FhirJson.Parse<OperationOutcome>(await response.Content.ReadAsStringAsync());
        Assert.Contains(outcome.Issue, i => i.Severity == OperationOutcome.IssueSeverity.Error
            && (i.Details?.Text ?? "").Contains("identifier"));
    }

    [Fact]
    public async Task POST_validate_accepts_a_patient_satisfying_us_core()
    {
        var request = new Patient { Active = true };
        request.Name.Add(new HumanName { Use = HumanName.NameUse.Official, Family = "Doe", Given = ["Jane"] });
        request.Gender = AdministrativeGender.Female;
        request.Identifier.Add(new Identifier { System = "http://hl7.org/fhir/sid/us-ssn", Value = "123-45-6789" });

        var response = await _client!.PostAsync("/fhir/Patient/$validate", FhirBody(request));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var outcome = FhirJson.Parse<OperationOutcome>(await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain(outcome.Issue, i => i.Severity is OperationOutcome.IssueSeverity.Error or OperationOutcome.IssueSeverity.Fatal);
    }
}
