using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenEmrNext.ClinicalCare.Api.Contracts.Requests;
using OpenEmrNext.ClinicalCare.Api.Contracts.Responses;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// <c>PUT</c>, <c>DELETE</c> and <c>GET /api/v1/encounters/{uuid}/issues</c> through the real HTTP pipeline (auth, binding, use case, adapters, database), as the production
/// principal (QLT-9). Status codes follow API-3: a new link is 201, an existing one 200, an unlink is 204 either way.
/// </summary>
[Collection("ClinicalCareContainer")]
public class EncounterIssueEndpointTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    private static readonly Guid OtherPatient = Guid.Parse("b1a9c5f0-0000-4000-8000-0000000000bb");

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private Guid _encounter;
    private Guid _problem;
    private Guid _allergy;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await fixture.SeedPatientAsync(EncounterTestData.PatientUuid, EncounterTestData.PatientPid);
        await fixture.SeedPatientAsync(OtherPatient, 99);
        foreach (var o in new[] { "0", "1" })
        {
            await fixture.RootExecuteAsync($"INSERT INTO list_options (list_id, option_id, title, seq, is_default) VALUES ('outcome', '{o}', 't', {o}, 0)");
            await fixture.RootExecuteAsync($"INSERT INTO list_options (list_id, option_id, title, seq, is_default) VALUES ('occurrence', '{o}', 't', {o}, 0)");
        }

        await AllergyTestData.SeedCodeListsAsync(fixture);
        _factory = ApiFactory.Create(fixture);
        _client = _factory.ClientWithSecret();

        _encounter = (await (await _client.PostAsJsonAsync(
                "/api/v1/encounters", new CreateEncounterRequest(EncounterTestData.PatientUuid, null, 5, null, "visit", 3, null, 7, null, null)))
            .Content.ReadFromJsonAsync<EncounterCreatedResponse>())!.Uuid;
        _problem = (await (await _client.PostAsJsonAsync(
                "/api/v1/problems", new RecordProblemRequest(EncounterTestData.PatientUuid, "Asthma", new DateTime(2020, 5, 1), null, null, 0, 0, null, null, 7)))
            .Content.ReadFromJsonAsync<ProblemRecordedResponse>())!.Uuid;
        _allergy = (await (await _client.PostAsJsonAsync(
                "/api/v1/allergies", new RecordAllergyRequest(EncounterTestData.PatientUuid, "Penicillin", null, null, null, "hives", "severe", "confirmed", null, 7)))
            .Content.ReadFromJsonAsync<AllergyRecordedResponse>())!.Uuid;

        // A clean slate for the audit and outbox so each test counts only its own rows.
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_audit");
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_outbox");
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private string Url(Guid? encounter = null, Guid? issue = null) => $"/api/v1/encounters/{encounter ?? _encounter}/issues/{issue ?? _problem}";

    private Task<long> Count(string table, string where = "1=1") => fixture.RootScalarAsync<long>($"SELECT COUNT(*) FROM {table} WHERE {where}");

    private static async Task<string[]> ErrorsOf(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("errors").EnumerateObject().SelectMany(p => p.Value.EnumerateArray().Select(e => e.GetString()!)).ToArray();
    }

    [Fact]
    public async Task PUT_links_a_problem_and_returns_201_with_the_link_and_a_second_PUT_returns_200_with_the_same_one()
    {
        var first = await _client.PutAsJsonAsync(Url(), new LinkIssueRequest(Resolved: true));
        var second = await _client.PutAsJsonAsync(Url(), new LinkIssueRequest(Resolved: false));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var a = await first.Content.ReadFromJsonAsync<EncounterIssueLinkResponse>();
        var b = await second.Content.ReadFromJsonAsync<EncounterIssueLinkResponse>();
        Assert.Equal(_encounter, a!.EncounterUuid);
        Assert.Equal(_problem, a.IssueUuid);
        Assert.True(a.Resolved);
        Assert.Equal(a.LinkUuid, b!.LinkUuid);
        Assert.True(b.Resolved);   // a link is never updated: the stored flag comes back, not the one asked for
        Assert.Equal(1L, await Count("issue_encounter"));
    }

    [Fact]
    public async Task PUT_with_no_body_at_all_links_with_resolved_false()
    {
        var response = await _client.PutAsync(Url(issue: _allergy), content: null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.False((await response.Content.ReadFromJsonAsync<EncounterIssueLinkResponse>())!.Resolved);
    }

    [Fact]
    public async Task PUT_writes_a_legacy_shaped_row_an_event_and_an_audit_row_with_the_forwarded_address()
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, Url()) { Content = JsonContent.Create(new LinkIssueRequest(true)) };
        request.Headers.Add("X-Forwarded-For", "203.0.113.21");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1L, await Count("uuid_registry", "table_name = 'issue_encounter'"));
        Assert.Equal(1L, await Count("clinical_care_outbox", "event_type = 'IssueLinkedToEncounter'"));
        Assert.Equal("encounter.issue.link", await fixture.RootScalarAsync<string>("SELECT action FROM clinical_care_audit"));
        Assert.Equal(_encounter.ToString(), await fixture.RootScalarAsync<string>("SELECT CONCAT(subject, '') FROM clinical_care_audit"));
        Assert.Equal("203.0.113.21", await fixture.RootScalarAsync<string>("SELECT source_address FROM clinical_care_audit"));
        Assert.Null(await fixture.RootScalarAsync<long?>("SELECT created_by FROM issue_encounter"));
    }

    [Fact]
    public async Task PUT_a_repeated_link_writes_no_second_event_and_no_second_audit_row()
    {
        await _client.PutAsJsonAsync(Url(), new LinkIssueRequest());
        await _client.PutAsJsonAsync(Url(), new LinkIssueRequest());

        Assert.Equal(1L, await Count("clinical_care_outbox"));
        Assert.Equal(1L, await Count("clinical_care_audit"));
    }

    [Fact]
    public async Task PUT_for_an_unknown_encounter_or_an_unknown_issue_is_404_problem_json_and_leaves_a_failed_audit_row()
    {
        var noEncounter = await _client.PutAsJsonAsync(Url(encounter: Guid.NewGuid()), new LinkIssueRequest());
        var noIssue = await _client.PutAsJsonAsync(Url(issue: Guid.NewGuid()), new LinkIssueRequest());

        Assert.Equal(HttpStatusCode.NotFound, noEncounter.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, noIssue.StatusCode);
        Assert.Equal("application/problem+json", noIssue.Content.Headers.ContentType?.MediaType);
        Assert.Equal(2L, await Count("clinical_care_audit", "outcome = 'failed' AND action = 'encounter.issue.link'"));
        Assert.Equal(0L, await Count("issue_encounter"));
    }

    [Fact]
    public async Task ADR_0070_PUT_for_the_uuid_of_a_medication_or_a_surgery_is_404_because_this_service_owns_only_problems_and_allergies()
    {
        var medication = Guid.Parse("22222222-2222-4222-8222-222222222222");
        var surgery = Guid.Parse("33333333-3333-4333-8333-333333333333");
        await fixture.RootExecuteAsync(
            "INSERT INTO lists (uuid, type, title, pid, activity) VALUES " +
            $"(UNHEX('{medication:N}'), 'medication', 'Aspirin', 42, 1), (UNHEX('{surgery:N}'), 'surgery', 'Appendectomy', 42, 1)");

        Assert.Equal(HttpStatusCode.NotFound, (await _client.PutAsJsonAsync(Url(issue: medication), new LinkIssueRequest())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PutAsJsonAsync(Url(issue: surgery), new LinkIssueRequest())).StatusCode);
        Assert.Equal(0L, await Count("issue_encounter"));
    }

    [Fact]
    public async Task IE_3_PUT_of_another_patients_issue_is_400_and_writes_nothing_which_legacy_would_have_allowed()
    {
        var theirs = Guid.Parse("44444444-4444-4444-8444-444444444444");
        await fixture.RootExecuteAsync($"INSERT INTO lists (uuid, type, title, pid, activity) VALUES (UNHEX('{theirs:N}'), 'medical_problem', 'Someone elses', 99, 1)");

        var response = await _client.PutAsJsonAsync(Url(issue: theirs), new LinkIssueRequest());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("The issue belongs to a different patient than the encounter.", await ErrorsOf(response));
        Assert.Equal(0L, await Count("issue_encounter"));
        Assert.Equal(1L, await Count("clinical_care_audit", "outcome = 'failed'"));
    }

    [Fact]
    public async Task PUT_for_an_encounter_legacy_has_soft_deleted_is_404()
    {
        await fixture.RootExecuteAsync("UPDATE forms SET deleted = 1 WHERE formdir = 'newpatient'");

        Assert.Equal(HttpStatusCode.NotFound, (await _client.PutAsJsonAsync(Url(), new LinkIssueRequest())).StatusCode);
    }

    [Fact]
    public async Task GET_lists_the_linked_problems_and_allergies_with_their_kind_title_and_resolved_flag()
    {
        await _client.PutAsJsonAsync(Url(issue: _problem), new LinkIssueRequest(Resolved: true));
        await _client.PutAsJsonAsync(Url(issue: _allergy), new LinkIssueRequest());

        var response = await _client.GetAsync($"/api/v1/encounters/{_encounter}/issues");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<EncounterIssueListResponse>();
        Assert.Equal(2, body!.Total);
        Assert.Equal(25, body.Limit);
        var problem = Assert.Single(body.Items, i => i.Kind == "problem");
        Assert.Equal(_problem, problem.IssueUuid);
        Assert.Equal("Asthma", problem.Title);
        Assert.True(problem.Resolved);
        var allergy = Assert.Single(body.Items, i => i.Kind == "allergy");
        Assert.Equal(_allergy, allergy.IssueUuid);
        Assert.False(allergy.Resolved);
    }

    [Fact]
    public async Task GET_with_an_oversized_page_is_400_and_for_an_unknown_encounter_404_and_an_unlinked_encounter_is_an_empty_page()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync($"/api/v1/encounters/{_encounter}/issues?limit=10000")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/encounters/{Guid.NewGuid()}/issues")).StatusCode);
        var empty = await (await _client.GetAsync($"/api/v1/encounters/{_encounter}/issues")).Content.ReadFromJsonAsync<EncounterIssueListResponse>();
        Assert.Empty(empty!.Items);
        Assert.Equal(0, empty.Total);
    }

    [Fact]
    public async Task DELETE_removes_the_link_and_returns_204_and_a_second_DELETE_is_204_too_and_writes_nothing()
    {
        await _client.PutAsJsonAsync(Url(), new LinkIssueRequest());
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_audit");
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_outbox");

        var first = await _client.DeleteAsync(Url());
        var second = await _client.DeleteAsync(Url());

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Equal(0L, await Count("issue_encounter"));
        Assert.Equal(1L, await Count("clinical_care_audit", "action = 'encounter.issue.unlink' AND outcome = 'success'"));
        Assert.Equal(1L, await Count("clinical_care_outbox", "event_type = 'IssueUnlinkedFromEncounter'"));
    }

    [Fact]
    public async Task DELETE_removes_only_the_link_and_never_the_issue_or_the_encounter()
    {
        await _client.PutAsJsonAsync(Url(), new LinkIssueRequest());

        await _client.DeleteAsync(Url());

        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/api/v1/problems/{_problem}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/api/v1/encounters/{_encounter}")).StatusCode);
    }

    [Fact]
    public async Task DELETE_for_an_unknown_encounter_or_issue_is_404_and_a_failed_audit_row()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync(Url(encounter: Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync(Url(issue: Guid.NewGuid()))).StatusCode);

        Assert.Equal(2L, await Count("clinical_care_audit", "outcome = 'failed' AND action = 'encounter.issue.unlink'"));
    }

    [Fact]
    public async Task A_link_can_be_changed_by_unlinking_and_linking_again()
    {
        await _client.PutAsJsonAsync(Url(), new LinkIssueRequest(Resolved: false));
        await _client.DeleteAsync(Url());
        var relinked = await _client.PutAsJsonAsync(Url(), new LinkIssueRequest(Resolved: true));

        Assert.Equal(HttpStatusCode.Created, relinked.StatusCode);
        Assert.True((await relinked.Content.ReadFromJsonAsync<EncounterIssueLinkResponse>())!.Resolved);
    }

    [Fact]
    public async Task The_link_endpoints_need_the_gateway_secret()
    {
        using var anonymous = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsJsonAsync(Url(), new LinkIssueRequest())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync(Url())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/v1/encounters/{_encounter}/issues")).StatusCode);
        Assert.Equal(0L, await Count("issue_encounter"));
    }
}
