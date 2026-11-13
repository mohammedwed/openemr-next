using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using OpenEmrNext.PatientManagement.Api.Contracts.Requests;
using OpenEmrNext.PatientManagement.Api.Contracts.Responses;
using Xunit;

namespace OpenEmrNext.PatientManagement.IntegrationTests;

/// <summary>Exercises the real HTTP pipeline for the advance-directive endpoints, the same way
/// <see cref="InsuranceEndpointTests"/> does for insurance.</summary>
[Collection("PatientDataContainer")]
public class AdvanceDirectiveEndpointTests(PatientDataContainerFixture fixture) : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private readonly DirectoryInfo _repositoryRoot = Directory.CreateTempSubdirectory("advance-directive-endpoint-tests-");

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

    private static long NextPid() => Random.Shared.NextInt64(1, long.MaxValue);

    [Fact]
    public async Task POST_a_document_with_a_valid_body_returns_201()
    {
        var pid = NextPid();
        var request = new UploadAdvanceDirectiveDocumentRequest(
            "living_will", "consent.pdf", "application/pdf", Convert.ToBase64String([1, 2, 3]), new DateOnly(2026, 1, 1), 7);

        var response = await _client!.PostAsJsonAsync($"/api/v1/patients/{pid}/advance-directives/documents", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AdvanceDirectiveDocumentResponse>();
        Assert.NotNull(body);
        Assert.Equal("living_will", body!.Type);
        Assert.Equal("Living Will - consent.pdf", body.Name);
    }

    [Fact]
    public async Task POST_a_document_with_an_invalid_type_returns_400()
    {
        var pid = NextPid();
        var request = new UploadAdvanceDirectiveDocumentRequest(
            "not-a-type", "consent.pdf", "application/pdf", Convert.ToBase64String([1, 2, 3]), null, 7);

        var response = await _client!.PostAsJsonAsync($"/api/v1/patients/{pid}/advance-directives/documents", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GET_documents_returns_the_document_just_uploaded()
    {
        var pid = NextPid();
        await _client!.PostAsJsonAsync($"/api/v1/patients/{pid}/advance-directives/documents",
            new UploadAdvanceDirectiveDocumentRequest("dnr_order", "dnr.pdf", "application/pdf", Convert.ToBase64String([9]), null, 7));

        var response = await _client.GetAsync($"/api/v1/patients/{pid}/advance-directives/documents");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<AdvanceDirectiveDocumentResponse>>();
        Assert.NotNull(body);
        var document = Assert.Single(body!);
        Assert.Equal("dnr_order", document.Type);
    }

    [Fact]
    public async Task GET_document_content_returns_the_uploaded_bytes()
    {
        var pid = NextPid();
        var content = new byte[] { 1, 2, 3, 4, 5 };
        var uploaded = await _client!.PostAsJsonAsync($"/api/v1/patients/{pid}/advance-directives/documents",
            new UploadAdvanceDirectiveDocumentRequest("living_will", "scan.pdf", "application/pdf", Convert.ToBase64String(content), null, 7));
        var document = await uploaded.Content.ReadFromJsonAsync<AdvanceDirectiveDocumentResponse>();

        var response = await _client.GetAsync($"/api/v1/patients/{pid}/advance-directives/documents/{document!.Uuid}/content");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(content, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task GET_document_content_with_an_unknown_uuid_returns_404()
    {
        var pid = NextPid();

        var response = await _client!.GetAsync($"/api/v1/patients/{pid}/advance-directives/documents/{Guid.NewGuid()}/content");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PUT_status_then_GET_status_round_trips()
    {
        // The status flags live on patient_data -- unlike documents, which have no foreign key,
        // setting them for a pid with no row there is a silent no-op, so this needs a real registered
        // patient.
        var registered = await _client!.PostAsJsonAsync("/api/v1/patients", new RegisterPatientRequest(
            "Test", $"Patient{Guid.NewGuid():N}", "female", new DateOnly(2000, 1, 1), null, null, 1));
        var pid = (await registered.Content.ReadFromJsonAsync<PatientRegisteredResponse>())!.Pid;
        var request = new SetAdvanceDirectiveStatusRequest("yes", new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), 3);

        var putResponse = await _client!.PutAsJsonAsync($"/api/v1/patients/{pid}/advance-directives/status", request);
        Assert.Equal(HttpStatusCode.NoContent, putResponse.StatusCode);

        var getResponse = await _client.GetAsync($"/api/v1/patients/{pid}/advance-directives/status");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var body = await getResponse.Content.ReadFromJsonAsync<AdvanceDirectiveStatusResponse>();
        Assert.NotNull(body);
        Assert.Equal("yes", body!.Status);
        Assert.Equal(3, body.AuthenticatorUserId);
    }

    [Fact]
    public async Task PUT_status_with_an_invalid_value_returns_400()
    {
        var pid = NextPid();
        var request = new SetAdvanceDirectiveStatusRequest("maybe", null, null);

        var response = await _client!.PutAsJsonAsync($"/api/v1/patients/{pid}/advance-directives/status", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
