using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenEmrNext.ClinicalCare.Api.Contracts.Requests;
using OpenEmrNext.ClinicalCare.Api.Contracts.Responses;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// Deleting vitals, a SOAP note and a clinical note (M5g, ADR 0076) through the real HTTP pipeline against a real MariaDB, as the production principal. A deletion is legacy's own flag, never a removed row; it needs
/// legacy's <c>admin/super</c> right; and it is conditional on the version the caller read, like every other write.
/// </summary>
[Collection("ClinicalCareContainer")]
public class RecordDeletionTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    private static readonly Guid PlainUserUuid = Guid.Parse("7e57c0de-0000-4000-8000-000000000008");

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _admin = null!;    // dr_who, user 7: holds admin/super (a rule seeded below)
    private HttpClient _plain = null!;    // nurse_joy, user 8: holds nothing
    private Guid _encounter;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await ClinicalNoteTestData.SeedCodesAsync(fixture);
        await fixture.SeedPatientAsync(EncounterTestData.PatientUuid, EncounterTestData.PatientPid);
        await fixture.RootExecuteAsync($"INSERT INTO users (id, username, uuid, active) VALUES (8, 'nurse_joy', UNHEX('{PlainUserUuid:N}'), 1)");

        // Legacy's own shape: an enabled rule allowing admin/super to the user dr_who by name. nurse_joy has no rule at all.
        await fixture.RootExecuteAsync("INSERT INTO gacl_acl (id, section_value, allow, enabled, updated_date) VALUES (1, 'user', 1, 1, 100)");
        await fixture.RootExecuteAsync("INSERT INTO gacl_aco_map (acl_id, section_value, value) VALUES (1, 'admin', 'super')");
        await fixture.RootExecuteAsync("INSERT INTO gacl_aro_map (acl_id, section_value, value) VALUES (1, 'users', 'dr_who')");

        _factory = ApiFactory.Create(fixture);
        _admin = _factory.ClientWithSecret();
        _plain = _factory.ClientWithSecret(PlainUserUuid);
        _encounter = await NewEncounterAsync();
    }

    public Task DisposeAsync()
    {
        _admin.Dispose();
        _plain.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private async Task<Guid> NewEncounterAsync() =>
        (await (await _admin.PostAsJsonAsync(
            "/api/v1/encounters", new CreateEncounterRequest(EncounterTestData.PatientUuid, null, 5, null, "visit", 3, null, 7, null, null)))
            .Content.ReadFromJsonAsync<EncounterCreatedResponse>())!.Uuid;

    /// <summary>One record of each kind, with the URL that reaches it and how to see that it is flagged. The three kinds are deleted the same way, so most tests run for all of them.</summary>
    public enum Kind { Vitals, Soap, ClinicalNote }

    private sealed record Created(string Url, string Table, string FlagSql, string ETag);

    private async Task<Created> CreateAsync(Kind kind, Guid? encounter = null, string suffix = "")
    {
        var e = encounter ?? _encounter;
        switch (kind)
        {
            case Kind.Vitals:
            {
                var uuid = (await (await _admin.PostAsJsonAsync(
                        $"/api/v1/encounters/{e}/vitals",
                        new RecordVitalsRequest(new QuantityRequest(70m, "kg"), new QuantityRequest(175m, "cm"), null, null, 120, 80, 64, null, null, null, null, "calm" + suffix, 7)))
                    .Content.ReadFromJsonAsync<VitalsRecordedResponse>())!.Uuid;
                var url = $"/api/v1/encounters/{e}/vitals/{uuid}";
                return new Created(url, "form_vitals", $"SELECT f.deleted FROM forms f JOIN form_vitals v ON v.id = f.form_id AND f.formdir = 'vitals' WHERE v.uuid = UNHEX('{uuid:N}')", await ETagAsync(url));
            }
            case Kind.Soap:
            {
                var recorded = await _admin.PostAsJsonAsync(
                    $"/api/v1/encounters/{e}/soap-notes", new RecordSoapNoteRequest("cough for 3 days" + suffix, "lungs clear", "viral URI", "rest and fluids", 7));
                Assert.Equal(HttpStatusCode.Created, recorded.StatusCode);
                var id = (await recorded.Content.ReadFromJsonAsync<SoapNoteRecordedResponse>())!.Id;
                var url = $"/api/v1/encounters/{e}/soap-notes/{id}";
                return new Created(url, "form_soap", $"SELECT f.deleted FROM forms f WHERE f.form_id = {id} AND f.formdir = 'soap'", await ETagAsync(url));
            }
            default:
            {
                var uuid = (await (await _admin.PostAsJsonAsync(
                        $"/api/v1/encounters/{e}/clinical-notes",
                        new RecordClinicalNoteRequest("progress_note", "cardiology", "Patient comfortable." + suffix, new DateTime(2026, 11, 10), 7)))
                    .Content.ReadFromJsonAsync<ClinicalNoteRecordedResponse>())!.Uuid;
                var url = $"/api/v1/encounters/{e}/clinical-notes/{uuid}";
                // For a clinical note the flag is activity (1 = live), so deleted is its negation: the same "1 means flagged" for every kind.
                return new Created(url, "form_clinical_notes", $"SELECT 1 - n.activity FROM form_clinical_notes n WHERE n.uuid = UNHEX('{uuid:N}')", await ETagAsync(url));
            }
        }
    }

    private async Task<string> ETagAsync(string url) => (await _admin.GetAsync(url)).Headers.ETag!.ToString();

    private static Task<HttpResponseMessage> DeleteAsync(HttpClient client, string url, string? ifMatch)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        if (ifMatch is not null)
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        return client.SendAsync(request);
    }

    private Task<long> Flag(Created c) => fixture.RootScalarAsync<long>(c.FlagSql);

    private Task<long> Audit(string action, string outcome) =>
        fixture.RootScalarAsync<long>($"SELECT COUNT(*) FROM clinical_care_audit WHERE action = '{action}' AND outcome = '{outcome}'");

    private static string Action(Kind kind) => kind switch { Kind.Vitals => "vitals.delete", Kind.Soap => "soap.delete", _ => "clinical_note.delete" };

    private static string EventType(Kind kind) => kind switch { Kind.Vitals => "VitalsDeleted", Kind.Soap => "SoapNoteDeleted", _ => "ClinicalNoteDeleted" };

    [Theory]
    [InlineData(Kind.Vitals)]
    [InlineData(Kind.Soap)]
    [InlineData(Kind.ClinicalNote)]
    public async Task A_superuser_deletes_a_record_by_flagging_it_and_it_is_gone_from_reads_but_not_from_the_table(Kind kind)
    {
        var c = await CreateAsync(kind);
        Assert.Equal(0, await Flag(c));

        var response = await DeleteAsync(_admin, c.Url, c.ETag);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(1, await Flag(c));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>($"SELECT COUNT(*) FROM {c.Table}"));   // never removed
        Assert.Equal(HttpStatusCode.NotFound, (await _admin.GetAsync(c.Url)).StatusCode);
    }

    [Theory]
    [InlineData(Kind.Vitals)]
    [InlineData(Kind.Soap)]
    [InlineData(Kind.ClinicalNote)]
    public async Task A_deletion_writes_an_audit_row_and_an_event_that_name_the_record_and_say_nothing_about_it(Kind kind)
    {
        var c = await CreateAsync(kind, suffix: " SECRET-CLINICAL-TEXT");
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_audit");
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_outbox");

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAsync(_admin, c.Url, c.ETag)).StatusCode);

        Assert.Equal(1L, await Audit(Action(kind), "success"));
        // The actor is the verified user (ADR 0075): their OpenEMR uuid, marked verified, with the numeric id legacy's columns hold.
        Assert.Equal(ClinicalCareContainerFixture.TestUserUuid.ToString(), await fixture.RootScalarAsync<string>($"SELECT actor FROM clinical_care_audit WHERE action = '{Action(kind)}'"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>($"SELECT actor_verified FROM clinical_care_audit WHERE action = '{Action(kind)}'"));
        Assert.Equal(7L, await fixture.RootScalarAsync<long>($"SELECT actor_id FROM clinical_care_audit WHERE action = '{Action(kind)}'"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>($"SELECT COUNT(*) FROM clinical_care_outbox WHERE event_type = '{EventType(kind)}'"));
        var payload = await fixture.RootScalarAsync<string>($"SELECT payload FROM clinical_care_outbox WHERE event_type = '{EventType(kind)}'");
        Assert.DoesNotContain("SECRET-CLINICAL-TEXT", payload);
        Assert.DoesNotContain("SECRET-CLINICAL-TEXT", await fixture.RootScalarAsync<string>("SELECT CONCAT_WS('|', subject, IFNULL(details, '')) FROM clinical_care_audit") ?? "");
    }

    [Theory]
    [InlineData(Kind.Vitals)]
    [InlineData(Kind.Soap)]
    [InlineData(Kind.ClinicalNote)]
    public async Task A_user_who_does_not_hold_admin_super_is_refused_and_nothing_changes(Kind kind)
    {
        var c = await CreateAsync(kind);

        var response = await DeleteAsync(_plain, c.Url, c.ETag);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, await Flag(c));
        Assert.Equal(HttpStatusCode.OK, (await _admin.GetAsync(c.Url)).StatusCode);
        Assert.Equal(1L, await Audit(Action(kind), "failed"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>($"SELECT COUNT(*) FROM clinical_care_outbox WHERE event_type = '{EventType(kind)}'"));
    }

    [Fact]
    public async Task A_refusal_for_permission_comes_before_anything_about_the_record_is_revealed()
    {
        // A record that does not exist, asked by a user who may not delete: 403, not 404, so permission is never a way to probe for records.
        var missing = await _plain.SendAsync(Delete($"/api/v1/encounters/{_encounter}/soap-notes/999999", "\"c-00000000000000000000000000000000\""));
        var missingEncounter = await _plain.SendAsync(Delete($"/api/v1/encounters/{Guid.NewGuid()}/vitals/{Guid.NewGuid()}", "\"1\""));

        Assert.Equal(HttpStatusCode.Forbidden, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, missingEncounter.StatusCode);
    }

    private static HttpRequestMessage Delete(string url, string ifMatch)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        return request;
    }

    [Fact]
    public async Task A_right_taken_away_in_legacy_stops_a_deletion_on_the_very_next_request()
    {
        var c = await CreateAsync(Kind.Vitals);
        await fixture.RootExecuteAsync("UPDATE gacl_acl SET enabled = 0 WHERE id = 1");

        Assert.Equal(HttpStatusCode.Forbidden, (await DeleteAsync(_admin, c.Url, c.ETag)).StatusCode);
        Assert.Equal(0, await Flag(c));
    }

    [Theory]
    [InlineData(Kind.Vitals, null)]
    [InlineData(Kind.Vitals, "*")]
    [InlineData(Kind.Vitals, "  ")]
    [InlineData(Kind.Soap, null)]
    [InlineData(Kind.Soap, "*")]
    [InlineData(Kind.ClinicalNote, null)]
    [InlineData(Kind.ClinicalNote, "*")]
    public async Task A_deletion_with_no_usable_if_match_is_428_and_changes_nothing(Kind kind, string? ifMatch)
    {
        var c = await CreateAsync(kind);

        Assert.Equal(HttpStatusCode.PreconditionRequired, (await DeleteAsync(_admin, c.Url, ifMatch)).StatusCode);
        Assert.Equal(0, await Flag(c));
    }

    [Theory]
    [InlineData(Kind.Vitals)]
    [InlineData(Kind.Soap)]
    [InlineData(Kind.ClinicalNote)]
    public async Task A_record_changed_since_it_was_read_is_412_with_its_current_version_and_is_not_deleted(Kind kind)
    {
        var c = await CreateAsync(kind);
        await fixture.RootExecuteAsync(kind switch
        {
            Kind.Vitals => "UPDATE form_vitals SET note = 'edited in legacy', last_updated = '2030-01-01 00:00:00'",
            Kind.Soap => "UPDATE form_soap SET plan = 'edited in legacy'",
            _ => "UPDATE form_clinical_notes SET description = 'edited in legacy', last_updated = '2030-01-01 00:00:00'"
        });

        var response = await DeleteAsync(_admin, c.Url, c.ETag);

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        Assert.Equal(await ETagAsync(c.Url), response.Headers.ETag?.ToString());   // the version to reload from
        Assert.Equal(0, await Flag(c));
        Assert.Equal(1L, await Audit(Action(kind), "failed"));
    }

    [Theory]
    [InlineData(Kind.Vitals)]
    [InlineData(Kind.Soap)]
    [InlineData(Kind.ClinicalNote)]
    public async Task A_deletion_offering_a_malformed_version_is_412_not_a_server_error(Kind kind)
    {
        var c = await CreateAsync(kind);

        Assert.Equal(HttpStatusCode.PreconditionFailed, (await DeleteAsync(_admin, c.Url, "\"not-a-version\"")).StatusCode);
        Assert.Equal(0, await Flag(c));
    }

    [Theory]
    [InlineData(Kind.Vitals)]
    [InlineData(Kind.Soap)]
    [InlineData(Kind.ClinicalNote)]
    public async Task A_record_is_reachable_only_through_its_own_encounter(Kind kind)
    {
        var c = await CreateAsync(kind);
        var other = await NewEncounterAsync();
        var through = c.Url.Replace(_encounter.ToString(), other.ToString());

        Assert.Equal(HttpStatusCode.NotFound, (await DeleteAsync(_admin, through, c.ETag)).StatusCode);
        Assert.Equal(0, await Flag(c));
    }

    [Theory]
    [InlineData(Kind.Vitals)]
    [InlineData(Kind.Soap)]
    [InlineData(Kind.ClinicalNote)]
    public async Task Deleting_twice_is_404_the_second_time_and_writes_one_event_and_one_success_row(Kind kind)
    {
        var c = await CreateAsync(kind);
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_audit");
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_outbox");

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAsync(_admin, c.Url, c.ETag)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await DeleteAsync(_admin, c.Url, c.ETag)).StatusCode);

        Assert.Equal(1L, await Audit(Action(kind), "success"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>($"SELECT COUNT(*) FROM clinical_care_outbox WHERE event_type = '{EventType(kind)}'"));
    }

    [Theory]
    [InlineData(Kind.Vitals)]
    [InlineData(Kind.Soap)]
    [InlineData(Kind.ClinicalNote)]
    public async Task Two_simultaneous_deletions_of_one_record_succeed_once(Kind kind)
    {
        var c = await CreateAsync(kind);

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => DeleteAsync(_admin, c.Url, c.ETag)));

        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.NoContent));
        Assert.All(results.Where(r => r.StatusCode != HttpStatusCode.NoContent), r => Assert.Equal(HttpStatusCode.NotFound, r.StatusCode));
    }

    [Theory]
    [InlineData(Kind.Vitals)]
    [InlineData(Kind.Soap)]
    [InlineData(Kind.ClinicalNote)]
    public async Task Deleting_one_record_leaves_its_siblings_alone(Kind kind)
    {
        var first = await CreateAsync(kind, suffix: " first");
        var second = await CreateAsync(kind, suffix: " second");

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAsync(_admin, first.Url, first.ETag)).StatusCode);

        Assert.Equal(1, await Flag(first));
        Assert.Equal(0, await Flag(second));
        Assert.Equal(HttpStatusCode.OK, (await _admin.GetAsync(second.Url)).StatusCode);
    }

    [Fact]
    public async Task A_deleted_record_is_left_out_of_its_list()
    {
        var keep = await CreateAsync(Kind.Soap, suffix: " keep");
        var drop = await CreateAsync(Kind.Soap, suffix: " drop");

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAsync(_admin, drop.Url, drop.ETag)).StatusCode);

        var list = await _admin.GetFromJsonAsync<SoapNoteListResponse>($"/api/v1/encounters/{_encounter}/soap-notes");
        Assert.Equal(["cough for 3 days keep"], list!.Items.Select(n => n.Subjective));
        Assert.Equal(HttpStatusCode.OK, (await _admin.GetAsync(keep.Url)).StatusCode);
    }

    [Theory]
    [InlineData(Kind.Vitals)]
    [InlineData(Kind.Soap)]
    [InlineData(Kind.ClinicalNote)]
    public async Task A_record_no_longer_exists_to_be_corrected_once_deleted(Kind kind)
    {
        var c = await CreateAsync(kind);
        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAsync(_admin, c.Url, c.ETag)).StatusCode);

        var put = new HttpRequestMessage(HttpMethod.Put, c.Url) { Content = JsonContent.Create(new { }) };
        put.Headers.TryAddWithoutValidation("If-Match", c.ETag);
        Assert.NotEqual(HttpStatusCode.NoContent, (await _admin.SendAsync(put)).StatusCode);
        Assert.Equal(1, await Flag(c));
    }

    [Fact]
    public async Task A_deletion_needs_the_gateway_secret_and_a_verified_user()
    {
        var c = await CreateAsync(Kind.Vitals);
        using var anonymous = _factory.CreateClient();
        using var noUser = _factory.ClientWithSecretOnly();

        Assert.Equal(HttpStatusCode.Unauthorized, (await DeleteAsync(anonymous, c.Url, c.ETag)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await DeleteAsync(noUser, c.Url, c.ETag)).StatusCode);
        Assert.Equal(0, await Flag(c));
    }
}
