using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using OpenEmrNext.PatientManagement.Api.Contracts.Requests;
using OpenEmrNext.PatientManagement.Api.Contracts.Responses;
using Xunit;

namespace OpenEmrNext.PatientManagement.IntegrationTests;

/// <summary>
/// ADR 0075 at Patient Management: every <c>/api/v1</c> and <c>/fhir</c> route needs the user the gateway verified, resolved to a real, active legacy user, who becomes the one recorded as registering a patient,
/// uploading a document or reviewing an advance directive. The ids a request body names no longer count.
/// </summary>
[Collection("PatientDataContainer")]
public class VerifiedUserTests(PatientDataContainerFixture fixture) : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;

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
                new("InternalGateway:Secret", "test-secret")
            ])));
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _factory?.Dispose();
        return Task.CompletedTask;
    }

    private HttpClient Client(string? user)
    {
        var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Add("X-Internal-Gateway-Secret", "test-secret");
        if (user is not null)
            client.DefaultRequestHeaders.Add("X-Authenticated-User", user);
        return client;
    }

    private static string TestUser => PatientDataContainerFixture.TestUserUuid.ToString();

    private async Task<T?> ScalarAsync<T>(string sql)
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? default : (T)Convert.ChangeType(value, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T));
    }

    private static RegisterPatientRequest Registration(string last, long? registeredBy = null) =>
        new("Test", last, "Female", new DateOnly(1980, 1, 1), null, null, registeredBy);

    public static IEnumerable<object[]> Routes => [["GET", "/api/v1/patients"], ["POST", "/api/v1/patients"], ["GET", "/api/v1/patients/1/insurance"], ["GET", "/api/v1/patients/1/advance-directives/status"], ["GET", "/fhir/Patient"], ["POST", "/fhir/Patient"]];

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task A_request_with_the_secret_but_no_verified_user_is_401_on_every_route_whatever_else_is_wrong_with_it(string method, string path)
    {
        using var client = Client(user: null);
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST")
            request.Content = JsonContent.Create(new { });   // invalid on purpose: a 400 would mean the body was judged before the caller was

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request)).StatusCode);
    }

    [Theory]
    [InlineData("not-a-uuid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task A_malformed_identity_is_401(string header)
    {
        using var client = Client(header);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/patients")).StatusCode);
    }

    [Fact]
    public async Task A_uuid_that_matches_no_user_and_a_deactivated_account_are_403_and_nothing_is_registered()
    {
        var before = await ScalarAsync<long>("SELECT COUNT(*) FROM patient_data");
        using var stranger = Client(Guid.NewGuid().ToString());
        using var gone = Client(PatientDataContainerFixture.InactiveUserUuid.ToString());

        var unknown = await stranger.PostAsJsonAsync("/api/v1/patients", Registration($"Nobody{Guid.NewGuid():N}"));
        var inactive = await gone.PostAsJsonAsync("/api/v1/patients", Registration($"Nobody{Guid.NewGuid():N}"));

        Assert.Equal(HttpStatusCode.Forbidden, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, inactive.StatusCode);
        Assert.Equal(before, await ScalarAsync<long>("SELECT COUNT(*) FROM patient_data"));
    }

    [Fact]
    public async Task The_shared_secret_is_still_required_and_a_verified_user_alone_is_not_enough()
    {
        using var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Add("X-Authenticated-User", TestUser);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/patients")).StatusCode);
    }

    [Fact]
    public async Task A_patient_is_registered_by_the_verified_user_whatever_the_body_says_and_the_field_can_be_left_out()
    {
        using var client = Client(TestUser);
        var named = $"Named{Guid.NewGuid():N}";
        var omitted = $"Omitted{Guid.NewGuid():N}";

        var a = await client.PostAsJsonAsync("/api/v1/patients", Registration(named, registeredBy: 1));   // the body claims to be user 1
        var b = await client.PostAsJsonAsync("/api/v1/patients", Registration(omitted));

        Assert.Equal(HttpStatusCode.Created, a.StatusCode);
        Assert.Equal(HttpStatusCode.Created, b.StatusCode);
        Assert.Equal(PatientDataContainerFixture.TestUserId, await ScalarAsync<long>($"SELECT created_by FROM patient_data WHERE lname = '{named}'"));
        Assert.Equal(PatientDataContainerFixture.TestUserId, await ScalarAsync<long>($"SELECT created_by FROM patient_data WHERE lname = '{omitted}'"));
    }

    [Fact]
    public async Task A_document_is_owned_by_the_verified_user_and_a_status_review_names_them()
    {
        using var client = Client(TestUser);
        var registered = await client.PostAsJsonAsync("/api/v1/patients", Registration($"Directive{Guid.NewGuid():N}"));
        var pid = (await registered.Content.ReadFromJsonAsync<PatientRegisteredResponse>())!.Pid;

        var upload = await client.PostAsJsonAsync(
            $"/api/v1/patients/{pid}/advance-directives/documents",
            new UploadAdvanceDirectiveDocumentRequest("living_will", "will.pdf", "application/pdf", Convert.ToBase64String("pdf bytes"u8.ToArray()), null, OwnerId: 1));
        var status = await client.PutAsJsonAsync($"/api/v1/patients/{pid}/advance-directives/status", new SetAdvanceDirectiveStatusRequest("yes", null, AuthenticatorUserId: 1));

        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        Assert.Equal(PatientDataContainerFixture.TestUserId, (await upload.Content.ReadFromJsonAsync<AdvanceDirectiveDocumentResponse>())!.OwnerId);
        Assert.Equal(HttpStatusCode.NoContent, status.StatusCode);
        Assert.Equal(PatientDataContainerFixture.TestUserId, (await (await client.GetAsync($"/api/v1/patients/{pid}/advance-directives/status")).Content.ReadFromJsonAsync<AdvanceDirectiveStatusResponse>())!.AuthenticatorUserId);
    }

    [Fact]
    public async Task A_patient_created_through_fhir_is_registered_by_the_verified_user_whatever_the_query_says()
    {
        using var client = Client(TestUser);
        var last = $"Fhir{Guid.NewGuid():N}";
        var body = new StringContent($$"""{"resourceType":"Patient","name":[{"use":"official","family":"{{last}}","given":["Test"]}],"gender":"female","birthDate":"1980-01-01"}""", System.Text.Encoding.UTF8, "application/fhir+json");

        var response = await client.PostAsync("/fhir/Patient?registeredBy=1", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(PatientDataContainerFixture.TestUserId, await ScalarAsync<long>($"SELECT created_by FROM patient_data WHERE lname = '{last}'"));
    }
}
