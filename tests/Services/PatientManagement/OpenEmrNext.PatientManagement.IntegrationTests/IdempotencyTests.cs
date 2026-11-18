using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using OpenEmrNext.PatientManagement.Api.Contracts.Requests;
using OpenEmrNext.PatientManagement.Api.Contracts.Responses;
using OpenEmrNext.PatientManagement.Application.Ports;
using Xunit;

namespace OpenEmrNext.PatientManagement.IntegrationTests;

/// <summary>
/// The <c>Idempotency-Key</c> header on creating requests (API-5, ADR 0084) through the real HTTP pipeline against a real MariaDB: a retry is answered, not repeated, and a failure never leaves a key stuck.
/// Mirrors Clinical Care's suite (ADR 0077), because the behaviour is meant to be identical. The fixture's database is shared and not reset between tests, so every test uses keys and names of its own and counts
/// only its own rows.
/// </summary>
[Collection("PatientDataContainer")]
public class IdempotencyTests(PatientDataContainerFixture fixture) : IAsyncLifetime
{
    private static readonly Guid OtherUserUuid = Guid.Parse("7e57c0de-0000-4000-8000-000000000008");
    private const string PatientsUrl = "/api/v1/patients";

    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;
    private HttpClient _other = null!;
    private HttpClient _noUser = null!;
    private readonly DirectoryInfo _repositoryRoot = Directory.CreateTempSubdirectory("pm-idempotency-tests-");

    public async Task InitializeAsync()
    {
        await ExecuteAsync($"INSERT IGNORE INTO users (id, uuid, active) VALUES (8, UNHEX('{OtherUserUuid:N}'), 1)");
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            [
                new("PatientManagementDb:Host", fixture.Hostname),
                new("PatientManagementDb:Port", fixture.Port.ToString()),
                new("PatientManagementDb:Database", fixture.Database),
                new("PatientManagementDb:User", fixture.User),
                new("PatientManagementDb:Password", fixture.DbPassword),
                new("AdvanceDirectiveStorage:RepositoryRoot", _repositoryRoot.FullName),
                new("InternalGateway:Secret", "test-secret")
            ])));
        _client = Client(PatientDataContainerFixture.TestUserUuid);
        _other = Client(OtherUserUuid);
        _noUser = Client(null);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _other.Dispose();
        _noUser.Dispose();
        _factory?.Dispose();
        _repositoryRoot.Delete(recursive: true);
        return Task.CompletedTask;
    }

    private HttpClient Client(Guid? user)
    {
        var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Add("X-Internal-Gateway-Secret", "test-secret");
        if (user is { } u)
            client.DefaultRequestHeaders.Add("X-Authenticated-User", u.ToString());
        return client;
    }

    private static string Token() => Guid.NewGuid().ToString("N");

    private static string RegisterJson(string lastName, string firstName = "Idem") =>
        JsonSerializer.Serialize(new RegisterPatientRequest(firstName, lastName, "female", new DateOnly(1991, 4, 4), null, null, 1), JsonSerializerOptions.Web);

    private static HttpRequestMessage Post(string url, string json, params string[] keys)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        foreach (var key in keys)
            request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        return request;
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string url, string json, params string[] keys) => client.SendAsync(Post(url, json, keys));

    private static bool Replayed(HttpResponseMessage r) => r.Headers.TryGetValues("Idempotent-Replayed", out var v) && v.Contains("true");

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private async Task<long> CountAsync(string table, string where = "1=1")
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table} WHERE {where}";
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    /// <summary>Plants a claim as an earlier request would have left it, with the fingerprint of exactly the request the test will send.</summary>
    private Task SeedClaimAsync(string key, string json, string state, int ageMinutes, string? body = null)
    {
        var fingerprint = Idempotency.Fingerprint("POST", PatientsUrl, Encoding.UTF8.GetBytes(json));
        var done = state == "done";
        return ExecuteAsync(
            $"""
            INSERT INTO patient_management_idempotency (actor_id, idem_key, fingerprint, state, claim_token, claimed_at, status_code, content_type, body)
            VALUES ({PatientDataContainerFixture.TestUserId}, '{key}', '{fingerprint}', '{state}', '{Guid.NewGuid()}', UTC_TIMESTAMP(6) - INTERVAL {ageMinutes} MINUTE,
                    {(done ? "201" : "NULL")}, {(done ? "'application/json'" : "NULL")}, {(body is null ? "NULL" : "'" + body + "'")})
            """);
    }

    private Task<long> PatientsNamed(string lastName) => CountAsync("patient_data", $"lname = '{lastName}'");

    [Fact]
    public async Task A_repeated_registration_with_the_same_key_is_answered_with_the_first_answer_and_creates_nothing_more()
    {
        var last = $"Replay{Token()}";
        var key = $"k-{Token()}";

        var first = await PostAsync(_client, PatientsUrl, RegisterJson(last), key);
        var second = await PostAsync(_client, PatientsUrl, RegisterJson(last), key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.False(Replayed(first));
        Assert.True(Replayed(second));
        Assert.Equal(first.Headers.Location, second.Headers.Location);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());
        var uuid = (await first.Content.ReadFromJsonAsync<PatientRegisteredResponse>())!.Uuid;
        Assert.Equal(1L, await PatientsNamed(last));
        Assert.Equal(1L, await CountAsync("patient_management_audit", $"action = 'patient.register' AND outcome = 'success' AND subject = '{uuid}'"));
        Assert.Equal(1L, await CountAsync("patient_management_outbox", $"event_type = 'PatientRegistered' AND payload LIKE '%{uuid}%'"));
    }

    [Fact]
    public async Task The_same_key_for_a_different_request_is_422_and_creates_nothing()
    {
        var key = $"k-{Token()}";
        var first = $"First{Token()}";
        var different = $"Different{Token()}";
        await PostAsync(_client, PatientsUrl, RegisterJson(first), key);

        var second = await PostAsync(_client, PatientsUrl, RegisterJson(different), key);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, second.StatusCode);
        Assert.Equal(1L, await PatientsNamed(first));
        Assert.Equal(0L, await PatientsNamed(different));
    }

    [Fact]
    public async Task The_same_key_and_body_sent_to_another_route_is_a_different_request()
    {
        var key = $"k-{Token()}";
        var json = RegisterJson($"Route{Token()}");
        var created = await PostAsync(_client, PatientsUrl, json, key);
        var pid = (await created.Content.ReadFromJsonAsync<PatientRegisteredResponse>())!.Pid;

        var elsewhere = await PostAsync(_client, $"{PatientsUrl}/{pid}/insurance", json, key);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, elsewhere.StatusCode);
    }

    [Fact]
    public async Task A_key_belongs_to_the_user_who_sent_it()
    {
        var key = $"shared-{Token()}";
        var last = $"Users{Token()}";

        var mine = await PostAsync(_client, PatientsUrl, RegisterJson(last), key);
        var theirs = await PostAsync(_other, PatientsUrl, RegisterJson(last, firstName: "Other"), key);

        Assert.Equal(HttpStatusCode.Created, mine.StatusCode);
        Assert.Equal(HttpStatusCode.Created, theirs.StatusCode);
        Assert.False(Replayed(theirs));   // never another user's answer
        Assert.NotEqual(await mine.Content.ReadAsStringAsync(), await theirs.Content.ReadAsStringAsync());
        Assert.Equal(2L, await PatientsNamed(last));
    }

    [Fact]
    public async Task Without_a_key_nothing_is_protected_and_nothing_is_stored()
    {
        var before = await CountAsync("patient_management_idempotency");
        var one = $"NoKeyA{Token()}";
        var two = $"NoKeyB{Token()}";

        await PostAsync(_client, PatientsUrl, RegisterJson(one));
        await PostAsync(_client, PatientsUrl, RegisterJson(two));

        Assert.Equal(1L, await PatientsNamed(one));
        Assert.Equal(1L, await PatientsNamed(two));
        Assert.Equal(before, await CountAsync("patient_management_idempotency"));
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("semi;colon")]
    [InlineData("quote'key")]
    [InlineData("a-key-that-is-far-too-long-to-be-accepted-because-it-is-over-sixty-four-chars")]
    public async Task A_malformed_key_is_400_and_nothing_is_created(string key)
    {
        var last = $"Bad{Token()}";

        var response = await PostAsync(_client, PatientsUrl, RegisterJson(last), key);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0L, await PatientsNamed(last));
    }

    [Fact]
    public async Task Two_keys_in_one_request_are_400()
    {
        var last = $"Two{Token()}";

        var response = await PostAsync(_client, PatientsUrl, RegisterJson(last), "one", "two");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0L, await PatientsNamed(last));
    }

    [Fact]
    public async Task A_refused_request_does_not_keep_its_key_so_a_corrected_retry_can_use_it()
    {
        var key = $"k-{Token()}";
        var last = $"Refused{Token()}";
        var bad = await PostAsync(_client, PatientsUrl, RegisterJson(last, firstName: ""), key);   // no first name: refused
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal(0L, await CountAsync("patient_management_idempotency", $"idem_key = '{key}'"));

        var corrected = await PostAsync(_client, PatientsUrl, RegisterJson(last, firstName: "Fixed"), key);

        Assert.Equal(HttpStatusCode.Created, corrected.StatusCode);
        Assert.False(Replayed(corrected));
        Assert.Equal(1L, await CountAsync("patient_management_idempotency", $"idem_key = '{key}' AND state = 'done'"));
    }

    [Fact]
    public async Task Many_simultaneous_requests_with_one_key_create_one_patient()
    {
        var key = $"k-{Token()}";
        var last = $"Race{Token()}";

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => PostAsync(_client, PatientsUrl, RegisterJson(last), key)));

        Assert.Equal(1L, await PatientsNamed(last));
        Assert.All(responses, r => Assert.True(r.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created && !Replayed(r));   // exactly one ran it
        var uuids = new List<Guid>();
        foreach (var created in responses.Where(r => r.StatusCode == HttpStatusCode.Created))
            uuids.Add((await created.Content.ReadFromJsonAsync<PatientRegisteredResponse>())!.Uuid);
        Assert.Single(uuids.Distinct());   // everyone who was answered was answered with the same patient
    }

    [Fact]
    public async Task A_request_still_running_is_409_with_a_retry_hint()
    {
        var key = $"k-{Token()}";
        var last = $"Busy{Token()}";
        var json = RegisterJson(last);
        await SeedClaimAsync(key, json, "started", ageMinutes: 1);

        var response = await PostAsync(_client, PatientsUrl, json, key);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.True(response.Headers.Contains("Retry-After"));
        Assert.Equal(0L, await PatientsNamed(last));
    }

    [Fact]
    public async Task A_claim_held_past_the_lease_by_a_request_that_never_finished_is_taken_over()
    {
        var key = $"k-{Token()}";
        var last = $"Dead{Token()}";
        var json = RegisterJson(last);
        await SeedClaimAsync(key, json, "started", ageMinutes: 10);

        var response = await PostAsync(_client, PatientsUrl, json, key);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1L, await PatientsNamed(last));
        Assert.Equal(1L, await CountAsync("patient_management_idempotency", $"idem_key = '{key}' AND state = 'done'"));
    }

    [Fact]
    public async Task An_answer_older_than_the_window_is_not_replayed()
    {
        var key = $"k-{Token()}";
        var last = $"Old{Token()}";
        var json = RegisterJson(last);
        var oldUuid = Guid.NewGuid();
        await SeedClaimAsync(key, json, "done", ageMinutes: 25 * 60, body: $"{{\"uuid\":\"{oldUuid}\"}}");

        var response = await PostAsync(_client, PatientsUrl, json, key);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.False(Replayed(response));
        Assert.NotEqual(oldUuid, (await response.Content.ReadFromJsonAsync<PatientRegisteredResponse>())!.Uuid);
        Assert.Equal(1L, await PatientsNamed(last));
    }

    [Fact]
    public async Task A_retried_insurance_policy_is_one_policy()
    {
        var registered = await (await PostAsync(_client, PatientsUrl, RegisterJson($"Policy{Token()}"))).Content.ReadFromJsonAsync<PatientRegisteredResponse>();
        var pid = registered!.Pid;
        var key = $"k-{Token()}";
        var policy = JsonSerializer.Serialize(new AddInsurancePolicyRequest("primary", 7, "POL-IDEM", null, null, new DateOnly(2026, 1, 1), null), JsonSerializerOptions.Web);

        var first = await PostAsync(_client, $"{PatientsUrl}/{pid}/insurance", policy, key);
        var second = await PostAsync(_client, $"{PatientsUrl}/{pid}/insurance", policy, key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.True(Replayed(second));
        Assert.Equal((await first.Content.ReadFromJsonAsync<InsurancePolicyAddedResponse>())!.Uuid, (await second.Content.ReadFromJsonAsync<InsurancePolicyAddedResponse>())!.Uuid);
        Assert.Equal(1L, await CountAsync("insurance_data", $"pid = {pid}"));
    }

    [Fact]
    public async Task A_key_on_a_document_upload_is_refused_not_quietly_ignored()
    {
        // The upload is a multipart file that can be far larger than a request can be fingerprinted at, so it takes no key. Honouring none while accepting one would leave a caller believing a retry was safe.
        var before = await CountAsync("patient_management_idempotency");

        var response = await PostAsync(_client, $"{PatientsUrl}/1/advance-directives/documents", "{}", $"k-{Token()}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("cannot carry an Idempotency-Key", await response.Content.ReadAsStringAsync());
        Assert.Equal(before, await CountAsync("patient_management_idempotency"));
    }

    [Fact]
    public async Task A_key_on_a_read_is_ignored()
    {
        var before = await CountAsync("patient_management_idempotency");
        var request = new HttpRequestMessage(HttpMethod.Get, PatientsUrl);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", "not even valid because of spaces");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(before, await CountAsync("patient_management_idempotency"));
    }

    [Fact]
    public async Task A_key_does_not_get_a_caller_past_sign_in_and_nothing_is_stored_for_one_who_is_not()
    {
        var before = await CountAsync("patient_management_idempotency");

        var response = await PostAsync(_noUser, PatientsUrl, RegisterJson($"Anon{Token()}"), $"k-{Token()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(before, await CountAsync("patient_management_idempotency"));
    }

    [Fact]
    public async Task The_store_holds_a_hash_and_the_answer_not_the_request()
    {
        const string marker = "Zqxsecretmark";   // letters only, so registration's name rules accept it
        var key = $"k-{Token()}";

        await PostAsync(_client, PatientsUrl, RegisterJson($"Priv{Token()}", firstName: marker), key);

        Assert.Equal(1L, await CountAsync("patient_management_idempotency", $"idem_key = '{key}'"));
        Assert.Equal(0L, await CountAsync("patient_management_idempotency",
            $"idem_key = '{key}' AND CONCAT_WS('|', fingerprint, IFNULL(body, ''), IFNULL(location, '')) LIKE '%{marker}%'"));
    }
}
