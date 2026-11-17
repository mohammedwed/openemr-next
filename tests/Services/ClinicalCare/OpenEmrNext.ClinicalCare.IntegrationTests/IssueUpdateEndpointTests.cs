using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenEmrNext.ClinicalCare.Api.Contracts.Requests;
using OpenEmrNext.ClinicalCare.Api.Contracts.Responses;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// <c>PUT /api/v1/problems/{uuid}</c> through the real HTTP pipeline, as the production principal (QLT-9): 200 with the new <c>ETag</c>; 428 with no <c>If-Match</c>; 412
/// when the record moved on, including when another writer changed it; 400 for invalid input; 404 for a record that is not there or is another issue type's (ADR 0068, 0070).
/// </summary>
[Collection("ClinicalCareContainer")]
public class ProblemUpdateEndpointTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    private static readonly DateTime Stamp = new(2026, 1, 2, 3, 4, 5);
    private static readonly string StampETag = ConcurrencyToken.From(Stamp).ToETag();

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private Guid _uuid;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await fixture.SeedPatientAsync(EncounterTestData.PatientUuid, EncounterTestData.PatientPid);
        foreach (var o in new[] { "0", "1", "2", "3", "4", "5" })
            await fixture.RootExecuteAsync($"INSERT INTO list_options (list_id, option_id, title, seq, is_default) VALUES ('outcome', '{o}', 't', {o}, 0)");
        foreach (var o in new[] { "0", "1", "4", "5" })
            await fixture.RootExecuteAsync($"INSERT INTO list_options (list_id, option_id, title, seq, is_default) VALUES ('occurrence', '{o}', 't', {o}, 0)");
        foreach (var o in new[] { "unconfirmed", "confirmed", "refuted", "entered-in-error" })
            await fixture.RootExecuteAsync($"INSERT INTO list_options (list_id, option_id, title, seq, is_default) VALUES ('condition-verification', '{o}', 't', 1, 0)");
        _factory = ApiFactory.Create(fixture);
        _client = _factory.ClientWithSecret();

        var created = await (await _client.PostAsJsonAsync(
                "/api/v1/problems",
                new RecordProblemRequest(EncounterTestData.PatientUuid, "Asthma", new DateTime(2020, 5, 1), null, "ICD10:J45.909", 0, 1, null, "seasonal", 7, "confirmed")))
            .Content.ReadFromJsonAsync<ProblemRecordedResponse>();
        _uuid = created!.Uuid;

        // A known stamp in the past, so "the tag changed" is decided by the update and not by what second the test ran in; and a clean audit and outbox so
        // each test counts only its own rows.
        await fixture.RootExecuteAsync($"UPDATE lists SET modifydate = '{Stamp:yyyy-MM-dd HH:mm:ss}'");
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_audit");
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_outbox");
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private static UpdateProblemRequest Body(
        string? title = "Asthma, severe", DateTime? begin = null, DateTime? end = null, string? diagnosis = "ICD10:J45.909", int outcome = 0, int occurrence = 1,
        string? referredBy = null, string? comments = "seasonal", string? verification = "confirmed") =>
        new(title, begin ?? new DateTime(2020, 5, 1), end, diagnosis, outcome, occurrence, referredBy, comments, verification);

    private Task<HttpResponseMessage> PutAsync(UpdateProblemRequest body, string? ifMatch = null, Guid? uuid = null, HttpClient? client = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/problems/{uuid ?? _uuid}") { Content = JsonContent.Create(body) };
        if (ifMatch is not null)
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        return (client ?? _client).SendAsync(request);
    }

    private Task<long> Count(string table, string where = "1=1") => fixture.RootScalarAsync<long>($"SELECT COUNT(*) FROM {table} WHERE {where}");

    private static async Task<string[]> ErrorsOf(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("errors").EnumerateObject().SelectMany(p => p.Value.EnumerateArray().Select(e => e.GetString()!)).ToArray();
    }

    [Fact]
    public async Task PUT_with_the_current_etag_returns_200_the_new_state_and_a_new_etag_that_a_following_GET_agrees_with()
    {
        var response = await PutAsync(Body(comments: "changed", verification: "refuted"), StampETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal(_uuid, body!.Uuid);
        Assert.Equal("Asthma, severe", body.Title);
        Assert.Equal("changed", body.Comments);
        Assert.Equal("refuted", body.Verification);
        var newETag = response.Headers.ETag?.ToString();
        Assert.NotNull(newETag);
        Assert.NotEqual(StampETag, newETag);
        Assert.Equal(newETag, (await _client.GetAsync($"/api/v1/problems/{_uuid}")).Headers.ETag?.ToString());
    }

    [Fact]
    public async Task A_problem_is_resolved_by_an_update_and_reopened_by_another()
    {
        var resolved = await PutAsync(Body(title: "Asthma", end: new DateTime(2022, 2, 2), outcome: 1), StampETag);
        await fixture.RootExecuteAsync($"UPDATE lists SET modifydate = '2026-03-04 05:06:07'");
        var reopened = await PutAsync(Body(title: "Asthma", end: null, outcome: 0), ConcurrencyToken.From(new DateTime(2026, 3, 4, 5, 6, 7)).ToETag());

        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        Assert.Equal(new DateTime(2022, 2, 2), (await resolved.Content.ReadFromJsonAsync<ProblemResponse>())!.EndDate);
        Assert.Equal(HttpStatusCode.OK, reopened.StatusCode);
        var body = await reopened.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Null(body!.EndDate);
        Assert.Equal(0, body.Outcome);
    }

    [Fact]
    public async Task PUT_with_no_if_match_a_wildcard_or_a_blank_one_is_428_and_writes_nothing()
    {
        foreach (var header in new string?[] { null, "*", "", "   " })
        {
            var response = await PutAsync(Body(), header);

            Assert.Equal((HttpStatusCode)428, response.StatusCode);
        }

        Assert.Equal("Asthma", await fixture.RootScalarAsync<string>("SELECT title FROM lists"));
        Assert.Equal(0L, await Count("clinical_care_audit"));
    }

    [Fact]
    public async Task PUT_with_a_stale_etag_is_412_carries_the_current_etag_and_writes_nothing()
    {
        var response = await PutAsync(Body(), ConcurrencyToken.From(Stamp.AddDays(-5)).ToETag());

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        Assert.Equal(StampETag, response.Headers.ETag?.ToString());   // so the caller can reload and try again
        Assert.Equal("Asthma", await fixture.RootScalarAsync<string>("SELECT title FROM lists"));
        Assert.Equal(0L, await Count("clinical_care_outbox"));
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("\"nonsense\"")]
    [InlineData("W/\"1\"")]
    public async Task PUT_with_an_unreadable_or_weak_tag_is_412_not_428(string ifMatch)
    {
        var response = await PutAsync(Body(), ifMatch);

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
    }

    [Fact]
    public async Task LEG_10_a_legacy_edit_after_the_read_makes_the_update_a_412_and_the_other_writers_value_survives()
    {
        var readTag = (await _client.GetAsync($"/api/v1/problems/{_uuid}")).Headers.ETag!.ToString();
        await fixture.RootExecuteAsync("UPDATE lists SET comments = 'edited in the issue screen'");

        var response = await PutAsync(Body(), readTag);

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        Assert.Equal("edited in the issue screen", await fixture.RootScalarAsync<string>("SELECT comments FROM lists"));
        Assert.NotEqual(readTag, response.Headers.ETag?.ToString());
    }

    [Fact]
    public async Task PUT_for_an_unknown_problem_is_404_problem_json()
    {
        var response = await PutAsync(Body(), StampETag, uuid: Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ADR_0070_PUT_problems_for_the_uuid_of_an_allergy_or_a_medication_is_404_and_changes_nothing()
    {
        var allergy = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var medication = Guid.Parse("22222222-2222-4222-8222-222222222222");
        await fixture.RootExecuteAsync(
            "INSERT INTO lists (uuid, type, title, pid, activity, modifydate) VALUES " +
            $"(UNHEX('{allergy:N}'), 'allergy', 'Penicillin', 42, 1, '{Stamp:yyyy-MM-dd HH:mm:ss}'), (UNHEX('{medication:N}'), 'medication', 'Aspirin', 42, 1, '{Stamp:yyyy-MM-dd HH:mm:ss}')");

        Assert.Equal(HttpStatusCode.NotFound, (await PutAsync(Body(), StampETag, uuid: allergy)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PutAsync(Body(), StampETag, uuid: medication)).StatusCode);
        Assert.Equal("Penicillin", await fixture.RootScalarAsync<string>("SELECT title FROM lists WHERE type = 'allergy'"));
        Assert.Equal("Aspirin", await fixture.RootScalarAsync<string>("SELECT title FROM lists WHERE type = 'medication'"));
    }

    [Fact]
    public async Task PUT_with_domain_rule_violations_is_400_with_every_message_and_writes_nothing()
    {
        var response = await PutAsync(Body(diagnosis: "bad", outcome: 9, occurrence: 2, verification: "differential", end: new DateTime(2019, 1, 1)), StampETag);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ErrorsOf(response);
        Assert.Contains("Outcome 9 is not a known outcome.", errors);
        Assert.Contains("Occurrence 2 is not a known occurrence.", errors);
        Assert.Contains("Verification 'differential' is not a known verification.", errors);
        Assert.Contains("End date cannot be before the begin date.", errors);
        Assert.Equal("Asthma", await fixture.RootScalarAsync<string>("SELECT title FROM lists"));
    }

    [Fact]
    public async Task The_order_of_checks_is_a_malformed_body_then_428_then_412_then_domain_rules()
    {
        var malformed = await PutAsync(Body(title: null));                                                       // no If-Match and no title
        var domainButNoPrecondition = await PutAsync(Body(title: "Asthma", outcome: 9));                        // no If-Match
        var domainButStale = await PutAsync(Body(outcome: 9), ConcurrencyToken.From(Stamp.AddDays(-5)).ToETag());

        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal((HttpStatusCode)428, domainButNoPrecondition.StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, domainButStale.StatusCode);
    }

    [Fact]
    public async Task PUT_with_no_begin_date_is_400_because_a_problem_requires_one()
    {
        var response = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Put, $"/api/v1/problems/{_uuid}")
        {
            Content = JsonContent.Create(new { title = "Asthma" }),
            Headers = { { "If-Match", StampETag } }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PUT_of_what_is_already_stored_is_200_with_the_same_etag_and_writes_no_event_or_audit_row()
    {
        var stored = await (await _client.GetAsync($"/api/v1/problems/{_uuid}")).Content.ReadFromJsonAsync<ProblemResponse>();
        var same = new UpdateProblemRequest(
            stored!.Title, stored.BeginDate, stored.EndDate, stored.Diagnosis, stored.Outcome, stored.Occurrence, stored.ReferredBy, stored.Comments, stored.Verification);

        var response = await PutAsync(same, StampETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(StampETag, response.Headers.ETag?.ToString());
        Assert.Equal(0L, await Count("clinical_care_outbox"));
        Assert.Equal(0L, await Count("clinical_care_audit"));
    }

    [Fact]
    public async Task SEC_9_a_successful_update_leaves_one_success_row_naming_fields_and_no_values_and_one_event_and_a_412_and_a_404_each_one_failed_row()
    {
        await PutAsync(Body(comments: "secret detail"), StampETag);
        await PutAsync(Body(), ConcurrencyToken.From(Stamp.AddDays(-5)).ToETag());
        await PutAsync(Body(), StampETag, uuid: Guid.NewGuid());

        Assert.Equal(1L, await Count("clinical_care_audit", "outcome = 'success' AND action = 'problem.update'"));
        Assert.Equal(2L, await Count("clinical_care_audit", "outcome = 'failed' AND action = 'problem.update'"));
        var details = await fixture.RootScalarAsync<string>("SELECT details FROM clinical_care_audit WHERE outcome = 'success'");
        Assert.Contains("comments", details);
        Assert.DoesNotContain("secret", details);
        Assert.Equal(1L, await Count("clinical_care_outbox", "event_type = 'ProblemUpdated'"));
    }

    [Fact]
    public async Task PUT_leaves_the_columns_it_does_not_own_as_they_were()
    {
        await fixture.RootExecuteAsync($"UPDATE lists SET udi = 'device-1', external_id = 'ext-9', modifydate = '{Stamp:yyyy-MM-dd HH:mm:ss}'");

        await PutAsync(Body(), StampETag);

        Assert.Equal("device-1", await fixture.RootScalarAsync<string>("SELECT udi FROM lists"));
        Assert.Equal("ext-9", await fixture.RootScalarAsync<string>("SELECT external_id FROM lists"));
        Assert.Equal("medical_problem", await fixture.RootScalarAsync<string>("SELECT type FROM lists"));
    }

    [Fact]
    public async Task PUT_with_no_gateway_secret_is_401_and_writes_nothing()
    {
        using var anonymous = _factory.CreateClient();

        var response = await PutAsync(Body(), StampETag, client: anonymous);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Asthma", await fixture.RootScalarAsync<string>("SELECT title FROM lists"));
    }

    [Fact]
    public async Task A_problem_can_be_recorded_with_a_verification_and_a_bad_one_is_400()
    {
        var ok = await _client.PostAsJsonAsync("/api/v1/problems", new RecordProblemRequest(EncounterTestData.PatientUuid, "Hypertension", new DateTime(2021, 1, 1), null, null, 0, 0, null, null, 7, "unconfirmed"));
        var bad = await _client.PostAsJsonAsync("/api/v1/problems", new RecordProblemRequest(EncounterTestData.PatientUuid, "Hypertension", new DateTime(2021, 1, 1), null, null, 0, 0, null, null, 7, "differential"));

        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Contains("Verification 'differential' is not a known verification.", await ErrorsOf(bad));
        var uuid = (await ok.Content.ReadFromJsonAsync<ProblemRecordedResponse>())!.Uuid;
        Assert.Equal("unconfirmed", (await (await _client.GetAsync($"/api/v1/problems/{uuid}")).Content.ReadFromJsonAsync<ProblemResponse>())!.Verification);
    }
}

/// <summary><c>PUT /api/v1/allergies/{uuid}</c> through the real HTTP pipeline: the same contract as the problem one, on the allergy fields and the allergy type.</summary>
[Collection("ClinicalCareContainer")]
public class AllergyUpdateEndpointTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    private static readonly DateTime Stamp = new(2026, 1, 2, 3, 4, 5);
    private static readonly string StampETag = ConcurrencyToken.From(Stamp).ToETag();

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private Guid _uuid;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await fixture.SeedPatientAsync(EncounterTestData.PatientUuid, EncounterTestData.PatientPid);
        await AllergyTestData.SeedCodeListsAsync(fixture);
        _factory = ApiFactory.Create(fixture);
        _client = _factory.ClientWithSecret();

        var created = await (await _client.PostAsJsonAsync(
                "/api/v1/allergies",
                new RecordAllergyRequest(EncounterTestData.PatientUuid, "Penicillin", null, null, null, "hives", "severe", "confirmed", "anaphylaxis", 7)))
            .Content.ReadFromJsonAsync<AllergyRecordedResponse>();
        _uuid = created!.Uuid;

        await fixture.RootExecuteAsync($"UPDATE lists SET modifydate = '{Stamp:yyyy-MM-dd HH:mm:ss}'");
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_audit");
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_outbox");
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private static UpdateAllergyRequest Body(
        string? title = "Penicillin", DateTime? end = null, string? reaction = "hives", string? severity = "severe", string? verification = "confirmed", string? comments = "anaphylaxis") =>
        new(title, null, end, null, reaction, severity, verification, comments);

    private Task<HttpResponseMessage> PutAsync(UpdateAllergyRequest body, string? ifMatch = null, Guid? uuid = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/allergies/{uuid ?? _uuid}") { Content = JsonContent.Create(body) };
        if (ifMatch is not null)
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        return _client.SendAsync(request);
    }

    private static async Task<string[]> ErrorsOf(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("errors").EnumerateObject().SelectMany(p => p.Value.EnumerateArray().Select(e => e.GetString()!)).ToArray();
    }

    [Fact]
    public async Task PUT_with_the_current_etag_returns_200_the_new_state_and_a_new_etag_that_a_following_GET_agrees_with()
    {
        var response = await PutAsync(Body(reaction: "nausea", severity: "mild", comments: "changed"), StampETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AllergyResponse>();
        Assert.Equal("nausea", body!.Reaction);
        Assert.Equal("mild", body.Severity);
        Assert.Equal("changed", body.Comments);
        var newETag = response.Headers.ETag?.ToString();
        Assert.NotEqual(StampETag, newETag);
        Assert.Equal(newETag, (await _client.GetAsync($"/api/v1/allergies/{_uuid}")).Headers.ETag?.ToString());
    }

    [Fact]
    public async Task An_allergy_is_ended_by_an_update_and_the_coded_fields_can_be_cleared()
    {
        var ended = await PutAsync(Body(end: new DateTime(2022, 2, 2), reaction: null, severity: null, verification: null), StampETag);

        Assert.Equal(HttpStatusCode.OK, ended.StatusCode);
        var body = await ended.Content.ReadFromJsonAsync<AllergyResponse>();
        Assert.Equal(new DateTime(2022, 2, 2), body!.EndDate);
        Assert.Null(body.Reaction);
        Assert.Null(body.Severity);
        Assert.Null(body.Verification);
        Assert.Equal("", await fixture.RootScalarAsync<string>("SELECT reaction FROM lists"));
    }

    [Fact]
    public async Task The_precondition_contract_is_428_without_a_usable_if_match_and_412_when_stale_or_unreadable()
    {
        Assert.Equal((HttpStatusCode)428, (await PutAsync(Body())).StatusCode);
        Assert.Equal((HttpStatusCode)428, (await PutAsync(Body(), "*")).StatusCode);
        var stale = await PutAsync(Body(), ConcurrencyToken.From(Stamp.AddDays(-5)).ToETag());
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal(StampETag, stale.Headers.ETag?.ToString());
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await PutAsync(Body(), "garbage")).StatusCode);
        Assert.Equal("Penicillin", await fixture.RootScalarAsync<string>("SELECT title FROM lists"));
    }

    [Fact]
    public async Task LEG_10_a_legacy_edit_after_the_read_makes_the_update_a_412_and_the_other_writers_value_survives()
    {
        var readTag = (await _client.GetAsync($"/api/v1/allergies/{_uuid}")).Headers.ETag!.ToString();
        await fixture.RootExecuteAsync("UPDATE lists SET comments = 'edited in the issue screen'");

        var response = await PutAsync(Body(title: "Amoxicillin"), readTag);

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        Assert.Equal("edited in the issue screen", await fixture.RootScalarAsync<string>("SELECT comments FROM lists"));
        Assert.Equal("Penicillin", await fixture.RootScalarAsync<string>("SELECT title FROM lists"));
    }

    [Fact]
    public async Task ADR_0070_PUT_allergies_for_the_uuid_of_a_problem_is_404_and_so_is_an_unknown_uuid()
    {
        var problem = Guid.Parse("11111111-1111-4111-8111-111111111111");
        await fixture.RootExecuteAsync($"INSERT INTO lists (uuid, type, title, pid, activity, modifydate) VALUES (UNHEX('{problem:N}'), 'medical_problem', 'Asthma', 42, 1, '{Stamp:yyyy-MM-dd HH:mm:ss}')");

        Assert.Equal(HttpStatusCode.NotFound, (await PutAsync(Body(), StampETag, uuid: problem)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PutAsync(Body(), StampETag, uuid: Guid.NewGuid())).StatusCode);
        Assert.Equal("Asthma", await fixture.RootScalarAsync<string>("SELECT title FROM lists WHERE type = 'medical_problem'"));
    }

    [Fact]
    public async Task PUT_with_domain_rule_violations_is_400_with_every_message_and_writes_nothing()
    {
        var response = await PutAsync(Body(reaction: "rash", severity: "catastrophic", verification: "differential"), StampETag);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ErrorsOf(response);
        Assert.Contains("Reaction 'rash' is not a known reaction.", errors);
        Assert.Contains("Severity 'catastrophic' is not a known severity.", errors);
        Assert.Contains("Verification 'differential' is not a known verification.", errors);
        Assert.Equal("hives", await fixture.RootScalarAsync<string>("SELECT reaction FROM lists"));
    }

    [Fact]
    public async Task The_order_of_checks_is_a_malformed_body_then_428_then_412_then_domain_rules()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await PutAsync(Body(title: null))).StatusCode);
        Assert.Equal((HttpStatusCode)428, (await PutAsync(Body(reaction: "rash"))).StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await PutAsync(Body(reaction: "rash"), ConcurrencyToken.From(Stamp.AddDays(-5)).ToETag())).StatusCode);
    }

    [Fact]
    public async Task PUT_of_what_is_already_stored_writes_no_event_or_audit_row_and_keeps_the_etag()
    {
        var response = await PutAsync(Body(), StampETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(StampETag, response.Headers.ETag?.ToString());
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_outbox"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_audit"));
    }

    [Fact]
    public async Task SEC_9_a_successful_update_leaves_one_success_row_with_field_names_only_and_a_412_leaves_one_failed_row()
    {
        await PutAsync(Body(reaction: "nausea"), StampETag);
        await PutAsync(Body(), ConcurrencyToken.From(Stamp.AddDays(-5)).ToETag());

        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_audit WHERE outcome = 'success' AND action = 'allergy.update'"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_audit WHERE outcome = 'failed' AND action = 'allergy.update'"));
        Assert.DoesNotContain("nausea", await fixture.RootScalarAsync<string>("SELECT details FROM clinical_care_audit WHERE outcome = 'success'"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_outbox WHERE event_type = 'AllergyUpdated'"));
    }

    [Fact]
    public async Task PUT_with_no_gateway_secret_is_401()
    {
        using var anonymous = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/allergies/{_uuid}") { Content = JsonContent.Create(Body()) };
        request.Headers.TryAddWithoutValidation("If-Match", StampETag);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.SendAsync(request)).StatusCode);
    }
}
