using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenEmrNext.ClinicalCare.Api.Contracts.Requests;
using OpenEmrNext.ClinicalCare.Api.Contracts.Responses;
using OpenEmrNext.ClinicalCare.Application.Ports;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// The <c>Idempotency-Key</c> header on creating requests (API-5, ADR 0077) through the real HTTP pipeline against a real MariaDB, as the production principal: a retry is answered, not repeated; a key is one user's; and
/// a failure never leaves a key stuck.
/// </summary>
[Collection("ClinicalCareContainer")]
public class IdempotencyTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    private static readonly Guid OtherUserUuid = Guid.Parse("7e57c0de-0000-4000-8000-000000000008");
    private const string EncountersUrl = "/api/v1/encounters";

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private HttpClient _other = null!;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await fixture.SeedPatientAsync(EncounterTestData.PatientUuid, EncounterTestData.PatientPid);
        await fixture.RootExecuteAsync($"INSERT INTO users (id, username, uuid, active) VALUES (8, 'nurse_joy', UNHEX('{OtherUserUuid:N}'), 1)");
        _factory = ApiFactory.Create(fixture);
        _client = _factory.ClientWithSecret();
        _other = _factory.ClientWithSecret(OtherUserUuid);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _other.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private static string NewEncounterJson(int category = 5, string reason = "visit") =>
        JsonSerializer.Serialize(new CreateEncounterRequest(EncounterTestData.PatientUuid, null, category, null, reason, 3, null, 7, null, null), JsonSerializerOptions.Web);

    private static HttpRequestMessage Post(string url, string json, params string[] keys)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        foreach (var key in keys)
            request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        return request;
    }

    private Task<HttpResponseMessage> PostAsync(HttpClient client, string url, string json, params string[] keys) => client.SendAsync(Post(url, json, keys));

    private static bool Replayed(HttpResponseMessage r) => r.Headers.TryGetValues("Idempotent-Replayed", out var v) && v.Contains("true");

    private Task<long> Count(string table, string where = "1=1") => fixture.RootScalarAsync<long>($"SELECT COUNT(*) FROM {table} WHERE {where}");

    [Fact]
    public async Task A_repeated_request_with_the_same_key_is_answered_with_the_first_answer_and_creates_nothing_more()
    {
        var first = await PostAsync(_client, EncountersUrl, NewEncounterJson(), "k-1");
        var second = await PostAsync(_client, EncountersUrl, NewEncounterJson(), "k-1");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.False(Replayed(first));
        Assert.True(Replayed(second));
        Assert.Equal(first.Headers.Location, second.Headers.Location);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());
        Assert.Equal(1L, await Count("form_encounter"));
        Assert.Equal(1L, await Count("clinical_care_audit", "action = 'encounter.create' AND outcome = 'success'"));
        Assert.Equal(1L, await Count("clinical_care_outbox", "event_type = 'EncounterCreated'"));
    }

    [Fact]
    public async Task The_same_key_for_a_different_request_is_422_and_creates_nothing()
    {
        await PostAsync(_client, EncountersUrl, NewEncounterJson(reason: "first"), "k-2");

        var second = await PostAsync(_client, EncountersUrl, NewEncounterJson(reason: "different"), "k-2");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, second.StatusCode);
        Assert.Equal(1L, await Count("form_encounter"));
    }

    [Fact]
    public async Task The_same_key_and_body_sent_to_another_route_is_a_different_request()
    {
        var created = await PostAsync(_client, EncountersUrl, NewEncounterJson(), "k-3");
        var uuid = (await created.Content.ReadFromJsonAsync<EncounterCreatedResponse>())!.Uuid;

        var elsewhere = await PostAsync(_client, $"/api/v1/encounters/{uuid}/soap-notes", NewEncounterJson(), "k-3");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, elsewhere.StatusCode);
    }

    [Fact]
    public async Task A_key_belongs_to_the_user_who_sent_it()
    {
        var mine = await PostAsync(_client, EncountersUrl, NewEncounterJson(), "shared-key");
        var theirs = await PostAsync(_other, EncountersUrl, NewEncounterJson(), "shared-key");

        Assert.Equal(HttpStatusCode.Created, mine.StatusCode);
        Assert.Equal(HttpStatusCode.Created, theirs.StatusCode);
        Assert.False(Replayed(theirs));   // never another user's answer
        Assert.NotEqual(await mine.Content.ReadAsStringAsync(), await theirs.Content.ReadAsStringAsync());
        Assert.Equal(2L, await Count("form_encounter"));
    }

    [Fact]
    public async Task Without_a_key_nothing_is_protected_and_nothing_is_stored()
    {
        await PostAsync(_client, EncountersUrl, NewEncounterJson());
        await PostAsync(_client, EncountersUrl, NewEncounterJson());

        Assert.Equal(2L, await Count("form_encounter"));
        Assert.Equal(0L, await Count("clinical_care_idempotency"));
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("semi;colon")]
    [InlineData("quote'key")]
    [InlineData("a-key-that-is-far-too-long-to-be-accepted-because-it-is-over-sixty-four-chars")]
    public async Task A_malformed_key_is_400_and_nothing_is_created(string key)
    {
        var response = await PostAsync(_client, EncountersUrl, NewEncounterJson(), key);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0L, await Count("form_encounter"));
    }

    [Fact]
    public async Task Two_keys_in_one_request_are_400()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(_client, EncountersUrl, NewEncounterJson(), "one", "two")).StatusCode);
        Assert.Equal(0L, await Count("form_encounter"));
    }

    [Fact]
    public async Task A_refused_request_does_not_keep_its_key_so_a_corrected_retry_can_use_it()
    {
        var bad = await PostAsync(_client, EncountersUrl, NewEncounterJson(category: 0), "k-6");
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal(0L, await Count("clinical_care_idempotency"));

        var corrected = await PostAsync(_client, EncountersUrl, NewEncounterJson(category: 5), "k-6");

        Assert.Equal(HttpStatusCode.Created, corrected.StatusCode);
        Assert.False(Replayed(corrected));
        Assert.Equal(1L, await Count("clinical_care_idempotency", "state = 'done'"));
    }

    [Fact]
    public async Task Many_simultaneous_requests_with_one_key_create_one_record()
    {
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => PostAsync(_client, EncountersUrl, NewEncounterJson(), "k-race")));

        Assert.Equal(1L, await Count("form_encounter"));
        Assert.All(responses, r => Assert.True(r.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created && !Replayed(r));   // exactly one ran it
        var uuids = new List<Guid>();
        foreach (var created in responses.Where(r => r.StatusCode == HttpStatusCode.Created))
            uuids.Add((await created.Content.ReadFromJsonAsync<EncounterCreatedResponse>())!.Uuid);
        Assert.Single(uuids.Distinct());   // everyone who was answered was answered with the same record
    }

    [Fact]
    public async Task A_request_still_running_is_409_with_a_retry_hint()
    {
        var json = NewEncounterJson();
        await SeedClaimAsync("k-busy", json, "started", ageMinutes: 1);

        var response = await PostAsync(_client, EncountersUrl, json, "k-busy");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.True(response.Headers.Contains("Retry-After"));
        Assert.Equal(0L, await Count("form_encounter"));
    }

    [Fact]
    public async Task A_claim_held_past_the_lease_by_a_request_that_never_finished_is_taken_over()
    {
        var json = NewEncounterJson();
        await SeedClaimAsync("k-dead", json, "started", ageMinutes: 10);

        var response = await PostAsync(_client, EncountersUrl, json, "k-dead");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1L, await Count("form_encounter"));
        Assert.Equal(1L, await Count("clinical_care_idempotency", "idem_key = 'k-dead' AND state = 'done'"));
    }

    [Fact]
    public async Task An_answer_older_than_the_window_is_not_replayed()
    {
        var json = NewEncounterJson();
        var oldUuid = Guid.NewGuid();
        await SeedClaimAsync("k-old", json, "done", ageMinutes: 25 * 60, body: $"{{\"uuid\":\"{oldUuid}\"}}");

        var response = await PostAsync(_client, EncountersUrl, json, "k-old");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.False(Replayed(response));
        Assert.NotEqual(oldUuid, (await response.Content.ReadFromJsonAsync<EncounterCreatedResponse>())!.Uuid);
        Assert.Equal(1L, await Count("form_encounter"));
    }

    [Fact]
    public async Task A_retried_soap_note_is_one_note()
    {
        var encounter = (await (await PostAsync(_client, EncountersUrl, NewEncounterJson())).Content.ReadFromJsonAsync<EncounterCreatedResponse>())!.Uuid;
        var note = JsonSerializer.Serialize(new RecordSoapNoteRequest("cough", "clear", "URI", "rest", 7), JsonSerializerOptions.Web);

        var first = await PostAsync(_client, $"/api/v1/encounters/{encounter}/soap-notes", note, "k-soap");
        var second = await PostAsync(_client, $"/api/v1/encounters/{encounter}/soap-notes", note, "k-soap");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.True(Replayed(second));
        Assert.Equal((await first.Content.ReadFromJsonAsync<SoapNoteRecordedResponse>())!.Id, (await second.Content.ReadFromJsonAsync<SoapNoteRecordedResponse>())!.Id);
        Assert.Equal(1L, await Count("form_soap"));
    }

    [Fact]
    public async Task Retried_vitals_are_one_record()
    {
        var encounter = (await (await PostAsync(_client, EncountersUrl, NewEncounterJson())).Content.ReadFromJsonAsync<EncounterCreatedResponse>())!.Uuid;
        var vitals = JsonSerializer.Serialize(
            new RecordVitalsRequest(new QuantityRequest(70m, "kg"), new QuantityRequest(175m, "cm"), null, null, 120, 80, 64, null, null, null, null, "calm", 7), JsonSerializerOptions.Web);

        await PostAsync(_client, $"/api/v1/encounters/{encounter}/vitals", vitals, "k-vit");
        var second = await PostAsync(_client, $"/api/v1/encounters/{encounter}/vitals", vitals, "k-vit");

        Assert.True(Replayed(second));
        Assert.Equal(1L, await Count("form_vitals"));
    }

    [Fact]
    public async Task A_key_on_a_read_is_ignored()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/lookups/facilities");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", "not even valid because of spaces");

        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(request)).StatusCode);
        Assert.Equal(0L, await Count("clinical_care_idempotency"));
    }

    [Fact]
    public async Task A_key_does_not_get_a_caller_past_sign_in_and_nothing_is_stored_for_one_who_is_not()
    {
        using var noUser = _factory.ClientWithSecretOnly();

        Assert.Equal(HttpStatusCode.Unauthorized, (await PostAsync(noUser, EncountersUrl, NewEncounterJson(), "k-anon")).StatusCode);
        Assert.Equal(0L, await Count("clinical_care_idempotency"));
    }

    [Fact]
    public async Task The_store_holds_a_hash_and_the_answer_not_the_request()
    {
        await PostAsync(_client, EncountersUrl, NewEncounterJson(reason: "SECRET-REASON-TEXT"), "k-priv");

        Assert.Equal(1L, await Count("clinical_care_idempotency"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>(
            "SELECT COUNT(*) FROM clinical_care_idempotency WHERE CONCAT_WS('|', fingerprint, IFNULL(body, ''), IFNULL(location, '')) LIKE '%SECRET-REASON-TEXT%'"));
    }

    /// <summary>Plants a claim as an earlier request would have left it, with the fingerprint of exactly the request the test will send.</summary>
    private async Task SeedClaimAsync(string key, string json, string state, int ageMinutes, string? body = null)
    {
        var fingerprint = Idempotency.Fingerprint("POST", EncountersUrl, Encoding.UTF8.GetBytes(json));
        var done = state == "done";
        await fixture.RootExecuteAsync(
            $"""
            INSERT INTO clinical_care_idempotency (actor_id, idem_key, fingerprint, state, claim_token, claimed_at, status_code, content_type, body)
            VALUES (7, '{key}', '{fingerprint}', '{state}', '{Guid.NewGuid()}', UTC_TIMESTAMP(6) - INTERVAL {ageMinutes} MINUTE,
                    {(done ? "201" : "NULL")}, {(done ? "'application/json'" : "NULL")}, {(body is null ? "NULL" : "'" + body + "'")})
            """);
    }
}
