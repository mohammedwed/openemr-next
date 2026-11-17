using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenEmrNext.ClinicalCare.Api.Contracts.Requests;
using OpenEmrNext.ClinicalCare.Api.Contracts.Responses;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// Deleting a problem or an allergy (M5n, ADR 0080) through the real HTTP pipeline against a real MariaDB, as the production principal. Unlike the form records, legacy removes the row, so this does: with its links
/// to encounters, only for a user who holds <c>admin/super</c>, conditional on the version the caller read, and never a row of another issue type.
/// </summary>
[Collection("ClinicalCareContainer")]
public class IssueDeletionTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    private static readonly Guid PlainUserUuid = Guid.Parse("7e57c0de-0000-4000-8000-000000000008");

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _admin = null!;
    private HttpClient _plain = null!;
    private Guid _encounter;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await fixture.SeedPatientAsync(EncounterTestData.PatientUuid, EncounterTestData.PatientPid);
        foreach (var o in new[] { "0", "1" })
        {
            await fixture.RootExecuteAsync($"INSERT INTO list_options (list_id, option_id, title, seq, is_default) VALUES ('outcome', '{o}', 't', {o}, 0)");
            await fixture.RootExecuteAsync($"INSERT INTO list_options (list_id, option_id, title, seq, is_default) VALUES ('occurrence', '{o}', 't', {o}, 0)");
        }

        await AllergyTestData.SeedCodeListsAsync(fixture);
        await fixture.RootExecuteAsync($"INSERT INTO users (id, username, uuid, active) VALUES (8, 'nurse_joy', UNHEX('{PlainUserUuid:N}'), 1)");
        await fixture.RootExecuteAsync("INSERT INTO gacl_acl (id, section_value, allow, enabled, updated_date) VALUES (1, 'user', 1, 1, 100)");
        await fixture.RootExecuteAsync("INSERT INTO gacl_aco_map (acl_id, section_value, value) VALUES (1, 'admin', 'super')");
        await fixture.RootExecuteAsync("INSERT INTO gacl_aro_map (acl_id, section_value, value) VALUES (1, 'users', 'dr_who')");

        _factory = ApiFactory.Create(fixture);
        _admin = _factory.ClientWithSecret();
        _plain = _factory.ClientWithSecret(PlainUserUuid);
        _encounter = (await (await _admin.PostAsJsonAsync(
                "/api/v1/encounters", new CreateEncounterRequest(EncounterTestData.PatientUuid, null, 5, null, "visit", 3, null, 7, null, null)))
            .Content.ReadFromJsonAsync<EncounterCreatedResponse>())!.Uuid;
    }

    public Task DisposeAsync()
    {
        _admin.Dispose();
        _plain.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    public enum Kind { Problem, Allergy }

    private sealed record Created(Guid Uuid, string Url, string Type, string ETag);

    private async Task<Created> CreateAsync(Kind kind, string title = "Asthma")
    {
        if (kind == Kind.Problem)
        {
            var uuid = (await (await _admin.PostAsJsonAsync("/api/v1/problems", new RecordProblemRequest(EncounterTestData.PatientUuid, title, new DateTime(2020, 5, 1), null, null, 0, 0, null, null, 7)))
                .Content.ReadFromJsonAsync<ProblemRecordedResponse>())!.Uuid;
            return await WithETagAsync(new Created(uuid, $"/api/v1/problems/{uuid}", "medical_problem", ""));
        }
        else
        {
            var uuid = (await (await _admin.PostAsJsonAsync("/api/v1/allergies", new RecordAllergyRequest(EncounterTestData.PatientUuid, title, null, null, null, "hives", "severe", "confirmed", null, 7)))
                .Content.ReadFromJsonAsync<AllergyRecordedResponse>())!.Uuid;
            return await WithETagAsync(new Created(uuid, $"/api/v1/allergies/{uuid}", "allergy", ""));
        }
    }

    private async Task<Created> WithETagAsync(Created c) => c with { ETag = (await _admin.GetAsync(c.Url)).Headers.ETag!.ToString() };

    private Task LinkAsync(Created c) => _admin.PutAsJsonAsync($"/api/v1/encounters/{_encounter}/issues/{c.Uuid}", new LinkIssueRequest(Resolved: false));

    private static Task<HttpResponseMessage> DeleteAsync(HttpClient client, string url, string? ifMatch)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        if (ifMatch is not null)
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        return client.SendAsync(request);
    }

    private Task<long> Count(string table, string where = "1=1") => fixture.RootScalarAsync<long>($"SELECT COUNT(*) FROM {table} WHERE {where}");

    private static string Action(Kind kind) => kind == Kind.Problem ? "problem.delete" : "allergy.delete";

    private static string EventType(Kind kind) => kind == Kind.Problem ? "ProblemDeleted" : "AllergyDeleted";

    [Theory]
    [InlineData(Kind.Problem)]
    [InlineData(Kind.Allergy)]
    public async Task A_superuser_deletes_the_issue_and_its_encounter_links_and_it_is_gone(Kind kind)
    {
        var c = await CreateAsync(kind);
        await LinkAsync(c);
        Assert.Equal(1L, await Count("issue_encounter"));

        var response = await DeleteAsync(_admin, c.Url, c.ETag);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0L, await Count("lists", $"type = '{c.Type}'"));
        Assert.Equal(0L, await Count("issue_encounter"));
        Assert.Equal(HttpStatusCode.NotFound, (await _admin.GetAsync(c.Url)).StatusCode);
    }

    [Theory]
    [InlineData(Kind.Problem)]
    [InlineData(Kind.Allergy)]
    public async Task A_deletion_writes_one_audit_row_and_one_event_that_name_the_record_and_say_nothing_about_it(Kind kind)
    {
        var c = await CreateAsync(kind, title: "SECRET-DIAGNOSIS-TEXT");
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_audit");
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_outbox");

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAsync(_admin, c.Url, c.ETag)).StatusCode);

        Assert.Equal(1L, await Count("clinical_care_audit", $"action = '{Action(kind)}' AND outcome = 'success' AND subject = '{c.Uuid}' AND actor_verified = 1 AND actor_id = 7"));
        Assert.Equal(1L, await Count("clinical_care_outbox", $"event_type = '{EventType(kind)}'"));
        var payload = await fixture.RootScalarAsync<string>($"SELECT payload FROM clinical_care_outbox WHERE event_type = '{EventType(kind)}'");
        Assert.DoesNotContain("SECRET-DIAGNOSIS-TEXT", payload);
        Assert.Contains(EncounterTestData.PatientUuid.ToString(), payload);   // a consumer holding a copy needs to know whose it was
    }

    [Theory]
    [InlineData(Kind.Problem)]
    [InlineData(Kind.Allergy)]
    public async Task A_user_without_admin_super_is_refused_and_nothing_changes(Kind kind)
    {
        var c = await CreateAsync(kind);
        await LinkAsync(c);

        var response = await DeleteAsync(_plain, c.Url, c.ETag);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1L, await Count("lists", $"type = '{c.Type}'"));
        Assert.Equal(1L, await Count("issue_encounter"));
        Assert.Equal(1L, await Count("clinical_care_audit", $"action = '{Action(kind)}' AND outcome = 'failed'"));
    }

    [Fact]
    public async Task Permission_is_checked_before_the_record_is_looked_up_so_a_refusal_reveals_nothing()
    {
        var missing = await DeleteAsync(_plain, $"/api/v1/problems/{Guid.NewGuid()}", "\"1\"");

        Assert.Equal(HttpStatusCode.Forbidden, missing.StatusCode);
    }

    [Theory]
    [InlineData(Kind.Problem, null)]
    [InlineData(Kind.Problem, "*")]
    [InlineData(Kind.Allergy, null)]
    [InlineData(Kind.Allergy, "*")]
    public async Task A_deletion_with_no_usable_if_match_is_428_and_changes_nothing(Kind kind, string? ifMatch)
    {
        var c = await CreateAsync(kind);

        Assert.Equal(HttpStatusCode.PreconditionRequired, (await DeleteAsync(_admin, c.Url, ifMatch)).StatusCode);
        Assert.Equal(1L, await Count("lists", $"type = '{c.Type}'"));
    }

    [Theory]
    [InlineData(Kind.Problem)]
    [InlineData(Kind.Allergy)]
    public async Task An_issue_changed_since_it_was_read_is_412_with_its_current_version_and_is_not_deleted(Kind kind)
    {
        var c = await CreateAsync(kind);
        await fixture.RootExecuteAsync($"UPDATE lists SET comments = 'edited in legacy', modifydate = '2030-01-01 00:00:00' WHERE uuid = UNHEX('{c.Uuid:N}')");

        var response = await DeleteAsync(_admin, c.Url, c.ETag);

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        Assert.Equal((await _admin.GetAsync(c.Url)).Headers.ETag?.ToString(), response.Headers.ETag?.ToString());
        Assert.Equal(1L, await Count("lists", $"type = '{c.Type}'"));
    }

    [Theory]
    [InlineData(Kind.Problem)]
    [InlineData(Kind.Allergy)]
    public async Task Deleting_twice_is_404_the_second_time_with_one_event_and_one_success_row(Kind kind)
    {
        var c = await CreateAsync(kind);
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_audit");
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_outbox");

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAsync(_admin, c.Url, c.ETag)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await DeleteAsync(_admin, c.Url, c.ETag)).StatusCode);

        Assert.Equal(1L, await Count("clinical_care_audit", $"action = '{Action(kind)}' AND outcome = 'success'"));
        Assert.Equal(1L, await Count("clinical_care_outbox", $"event_type = '{EventType(kind)}'"));
    }

    [Theory]
    [InlineData(Kind.Problem)]
    [InlineData(Kind.Allergy)]
    public async Task Six_simultaneous_deletions_succeed_once(Kind kind)
    {
        var c = await CreateAsync(kind);

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => DeleteAsync(_admin, c.Url, c.ETag)));

        Assert.Single(results, r => r.StatusCode == HttpStatusCode.NoContent);
        Assert.All(results.Where(r => r.StatusCode != HttpStatusCode.NoContent), r => Assert.Equal(HttpStatusCode.NotFound, r.StatusCode));
    }

    [Fact]
    public async Task A_problem_is_not_reachable_through_the_allergy_route_nor_an_allergy_through_the_problem_route()
    {
        var problem = await CreateAsync(Kind.Problem);
        var allergy = await CreateAsync(Kind.Allergy);

        Assert.Equal(HttpStatusCode.NotFound, (await DeleteAsync(_admin, $"/api/v1/allergies/{problem.Uuid}", problem.ETag)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await DeleteAsync(_admin, $"/api/v1/problems/{allergy.Uuid}", allergy.ETag)).StatusCode);
        Assert.Equal(2L, await Count("lists"));
    }

    [Theory]
    [InlineData("medication")]
    [InlineData("surgery")]
    [InlineData("medical_device")]
    public async Task A_row_of_another_issue_type_is_never_deleted_even_when_its_uuid_is_known(string otherType)
    {
        var uuid = Guid.NewGuid();
        await fixture.RootExecuteAsync(
            $"INSERT INTO lists (uuid, `date`, type, title, pid, activity) VALUES (UNHEX('{uuid:N}'), NOW(), '{otherType}', 'Not ours', {EncounterTestData.PatientPid}, 1)");

        var viaProblems = await DeleteAsync(_admin, $"/api/v1/problems/{uuid}", "\"1\"");
        var viaAllergies = await DeleteAsync(_admin, $"/api/v1/allergies/{uuid}", "\"1\"");

        Assert.Equal(HttpStatusCode.NotFound, viaProblems.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, viaAllergies.StatusCode);
        Assert.Equal(1L, await Count("lists", $"type = '{otherType}'"));
    }

    [Fact]
    public async Task Deleting_one_issue_leaves_the_patients_others_and_their_links_alone()
    {
        var keep = await CreateAsync(Kind.Problem, "Keep");
        var drop = await CreateAsync(Kind.Problem, "Drop");
        await LinkAsync(keep);
        await LinkAsync(drop);

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAsync(_admin, drop.Url, drop.ETag)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await _admin.GetAsync(keep.Url)).StatusCode);
        Assert.Equal(1L, await Count("issue_encounter"));
        Assert.Equal(1L, await Count("lists", "type = 'medical_problem'"));
    }

    [Fact]
    public async Task A_right_taken_away_in_legacy_stops_the_next_deletion()
    {
        var c = await CreateAsync(Kind.Problem);
        await fixture.RootExecuteAsync("UPDATE gacl_acl SET enabled = 0 WHERE id = 1");

        Assert.Equal(HttpStatusCode.Forbidden, (await DeleteAsync(_admin, c.Url, c.ETag)).StatusCode);
        Assert.Equal(1L, await Count("lists", "type = 'medical_problem'"));
    }

    [Fact]
    public async Task A_deletion_needs_the_gateway_secret_and_a_verified_user()
    {
        var c = await CreateAsync(Kind.Problem);
        using var anonymous = _factory.CreateClient();
        using var noUser = _factory.ClientWithSecretOnly();

        Assert.Equal(HttpStatusCode.Unauthorized, (await DeleteAsync(anonymous, c.Url, c.ETag)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await DeleteAsync(noUser, c.Url, c.ETag)).StatusCode);
        Assert.Equal(1L, await Count("lists", "type = 'medical_problem'"));
    }
}
