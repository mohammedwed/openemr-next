using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenEmrNext.ClinicalCare.Api.Contracts.Requests;
using OpenEmrNext.ClinicalCare.Api.Contracts.Responses;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// <c>/api/v1/encounters/{encounterUuid}/soap-notes</c> through the real HTTP pipeline, as the production principal (QLT-9, ADR 0073): record, read, list and correct, with the update contract carried by a content version
/// (ADR 0072): 200 with the new <c>ETag</c>, 428, 412, 400, 404 and 401. A note is addressed by its id within its encounter, and the encounter in the route is what authorises it.
/// </summary>
[Collection("ClinicalCareContainer")]
public class SoapNoteEndpointTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private Guid _encounter;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await fixture.SeedPatientAsync(EncounterTestData.PatientUuid, EncounterTestData.PatientPid);
        _factory = ApiFactory.Create(fixture);
        _client = _factory.ClientWithSecret();
        _encounter = await NewEncounterAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private async Task<Guid> NewEncounterAsync() =>
        (await (await _client.PostAsJsonAsync(
            "/api/v1/encounters", new CreateEncounterRequest(EncounterTestData.PatientUuid, null, 5, null, "visit", 3, null, 7, null, null)))
            .Content.ReadFromJsonAsync<EncounterCreatedResponse>())!.Uuid;

    private string Url(Guid? encounter = null, long? note = null) =>
        $"/api/v1/encounters/{encounter ?? _encounter}/soap-notes" + (note is null ? "" : $"/{note}");

    private static RecordSoapNoteRequest Record(string? s = "cough for 3 days", string? o = "lungs clear", string? a = "viral URI", string? p = "rest and fluids", long provider = 7) =>
        new(s, o, a, p, provider);

    private static CorrectSoapNoteRequest Correct(string? s = "cough for 3 days", string? o = "lungs clear", string? a = "viral URI", string? p = "rest, fluids and review") => new(s, o, a, p);

    private async Task<long> RecordedAsync(RecordSoapNoteRequest? request = null, Guid? encounter = null)
    {
        var response = await _client.PostAsJsonAsync(Url(encounter), request ?? Record());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SoapNoteRecordedResponse>())!.Id;
    }

    private async Task<string> ETagOfAsync(long id, Guid? encounter = null) => (await _client.GetAsync(Url(encounter, id))).Headers.ETag!.ToString();

    private Task<HttpResponseMessage> PutAsync(long id, CorrectSoapNoteRequest body, string? ifMatch = null, Guid? encounter = null, HttpClient? client = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, Url(encounter, id)) { Content = JsonContent.Create(body) };
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
    public async Task POST_records_a_note_201_with_a_location_and_GET_reads_it_back_with_an_etag_matching_its_body()
    {
        var response = await _client.PostAsJsonAsync(Url(), Record());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<SoapNoteRecordedResponse>();
        Assert.Equal($"/api/v1/encounters/{_encounter}/soap-notes/{created!.Id}", response.Headers.Location?.ToString());

        var read = await _client.GetAsync(Url(note: created.Id));
        var body = await read.Content.ReadFromJsonAsync<SoapNoteResponse>();
        Assert.Matches("^\"c-[0-9a-f]{32}\"$", read.Headers.ETag!.ToString());
        Assert.Equal(read.Headers.ETag.ToString(), body!.ETag);
        Assert.Equal("viral URI", body.Assessment);
        Assert.Equal("dr_who", body.RecordedBy);
    }

    [Fact]
    public async Task GET_of_an_unknown_note_or_unknown_encounter_is_404_and_a_note_is_not_reachable_through_another_encounter()
    {
        var id = await RecordedAsync();
        var other = await NewEncounterAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(Url(note: 987_654))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(Url(Guid.NewGuid(), id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(Url(other, id))).StatusCode);
    }

    [Fact]
    public async Task GET_list_returns_the_encounters_notes_newest_first_each_with_the_etag_a_correction_needs()
    {
        await RecordedAsync(Record(s: "first"));
        var latest = await RecordedAsync(Record(s: "second"));
        await RecordedAsync(Record(s: "on another encounter"), await NewEncounterAsync());

        var list = await _client.GetFromJsonAsync<SoapNoteListResponse>(Url());

        Assert.Equal(2, list!.Total);
        Assert.Equal(latest, list.Items[0].Id);
        Assert.Equal(await ETagOfAsync(latest), list.Items[0].ETag);
        Assert.Equal(HttpStatusCode.OK, (await PutAsync(latest, Correct(s: "second"), list.Items[0].ETag)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/encounters/{Guid.NewGuid()}/soap-notes")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync(Url() + "?limit=100000")).StatusCode);
    }

    [Fact]
    public async Task POST_with_no_text_or_a_too_short_or_too_long_section_is_400_with_every_message_and_records_nothing()
    {
        var empty = await _client.PostAsJsonAsync(Url(), Record(null, null, null, null));
        var bad = await _client.PostAsJsonAsync(Url(), Record(s: "x", p: new string('é', 40_000)));

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Contains("At least one section of the note is required.", await ErrorsOf(empty));
        var errors = await ErrorsOf(bad);
        Assert.Contains("Subjective must be at least 2 characters.", errors);
        Assert.Contains("Plan must be at most 65535 bytes.", errors);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync(Url(), Record(provider: 0))).StatusCode);
        Assert.Equal(0L, await Count("form_soap"));
        // Two refusals reached the use case and are audited; a bad ProviderId is refused at binding, before it, so it leaves no row.
        Assert.Equal(2L, await Count("clinical_care_audit", "outcome = 'failed' AND action = 'soap.record'"));
    }

    [Fact]
    public async Task POST_for_an_unknown_encounter_is_404_and_audited()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsJsonAsync(Url(Guid.NewGuid()), Record())).StatusCode);

        Assert.Equal(0L, await Count("form_soap"));
        Assert.Equal(1L, await Count("clinical_care_audit", "outcome = 'failed' AND action = 'soap.record'"));
    }

    [Fact]
    public async Task SEC_9_recording_leaves_one_success_row_keyed_by_the_note_naming_sections_and_no_text_and_one_event()
    {
        var id = await RecordedAsync(Record(s: "secret symptoms", o: null, a: null, p: null));

        Assert.Equal(1L, await Count("clinical_care_audit", $"action = 'soap.record' AND outcome = 'success' AND subject = 'soap:{id}'"));
        Assert.Equal("""["subjective"]""", await fixture.RootScalarAsync<string>("SELECT details FROM clinical_care_audit WHERE action = 'soap.record'"));
        Assert.DoesNotContain("secret", await fixture.RootScalarAsync<string>("SELECT payload FROM clinical_care_outbox WHERE event_type = 'SoapNoteRecorded'"));
    }

    [Fact]
    public async Task PUT_with_the_current_etag_returns_200_the_new_state_and_a_new_etag_that_a_following_GET_agrees_with()
    {
        var id = await RecordedAsync();
        var etag = await ETagOfAsync(id);

        var response = await PutAsync(id, Correct(), etag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SoapNoteResponse>();
        Assert.Equal("rest, fluids and review", body!.Plan);
        Assert.NotEqual(etag, response.Headers.ETag!.ToString());
        Assert.Equal(response.Headers.ETag.ToString(), body.ETag);
        Assert.Equal(body.ETag, await ETagOfAsync(id));
    }

    [Fact]
    public async Task PUT_can_clear_a_section_by_leaving_it_out_but_not_every_one()
    {
        var id = await RecordedAsync();

        var cleared = await PutAsync(id, Correct(p: null), await ETagOfAsync(id));
        var emptied = await PutAsync(id, new CorrectSoapNoteRequest(null, null, null, null), cleared.Headers.ETag!.ToString());

        Assert.Null((await cleared.Content.ReadFromJsonAsync<SoapNoteResponse>())!.Plan);
        Assert.Equal(HttpStatusCode.BadRequest, emptied.StatusCode);
    }

    [Fact]
    public async Task PUT_with_no_if_match_a_wildcard_or_a_blank_one_is_428_and_writes_nothing()
    {
        var id = await RecordedAsync();

        foreach (var header in new string?[] { null, "*", "", "   " })
            Assert.Equal((HttpStatusCode)428, (await PutAsync(id, Correct(), header)).StatusCode);

        Assert.Equal("rest and fluids", await fixture.RootScalarAsync<string>("SELECT plan FROM form_soap"));
    }

    [Fact]
    public async Task PUT_with_a_stale_unreadable_weak_or_stamp_shaped_etag_is_412_and_the_stale_one_carries_the_current_etag()
    {
        var id = await RecordedAsync();
        var current = await ETagOfAsync(id);

        var stale = await PutAsync(id, Correct(), ContentVersion.Of(["stale"]).ToETag());

        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal(current, stale.Headers.ETag?.ToString());
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await PutAsync(id, Correct(), "garbage")).StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await PutAsync(id, Correct(), "W/" + current)).StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await PutAsync(id, Correct(), ConcurrencyToken.From(DateTime.Now).ToETag())).StatusCode);
        Assert.Equal("rest and fluids", await fixture.RootScalarAsync<string>("SELECT plan FROM form_soap"));
    }

    [Fact]
    public async Task LEG_10_the_legacy_screen_editing_the_note_after_the_read_makes_the_correction_a_412_though_no_stamp_exists()
    {
        var id = await RecordedAsync();
        var readTag = await ETagOfAsync(id);
        await fixture.RootExecuteAsync("UPDATE form_soap SET objective = 'edited in the legacy screen'");

        var response = await PutAsync(id, Correct(), readTag);

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        Assert.Equal("edited in the legacy screen", await fixture.RootScalarAsync<string>("SELECT objective FROM form_soap"));
        Assert.NotEqual(readTag, response.Headers.ETag?.ToString());
    }

    [Fact]
    public async Task PUT_through_another_encounter_or_for_an_unknown_note_is_404_and_changes_nothing()
    {
        var id = await RecordedAsync();
        var etag = await ETagOfAsync(id);
        var other = await NewEncounterAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await PutAsync(id, Correct(), etag, other)).StatusCode);          // the other encounter's route cannot reach this note
        Assert.Equal(HttpStatusCode.NotFound, (await PutAsync(987_654, Correct(), etag)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PutAsync(id, Correct(), etag, Guid.NewGuid())).StatusCode);
        Assert.Equal("rest and fluids", await fixture.RootScalarAsync<string>("SELECT plan FROM form_soap"));
    }

    [Fact]
    public async Task The_order_of_checks_is_a_malformed_body_then_428_then_404_then_412_then_domain_rules()
    {
        var id = await RecordedAsync();
        var stale = ContentVersion.Of(["stale"]).ToETag();

        Assert.Equal((HttpStatusCode)428, (await PutAsync(id, Correct(s: "x"))).StatusCode);                                                    // domain-invalid but no precondition
        Assert.Equal(HttpStatusCode.NotFound, (await PutAsync(987_654, Correct(s: "x"), stale)).StatusCode);                                    // unknown note before a stale tag
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await PutAsync(id, Correct(s: "x"), stale)).StatusCode);                               // stale before domain rules
        Assert.Equal(HttpStatusCode.BadRequest, (await PutAsync(id, Correct(s: "x"), await ETagOfAsync(id))).StatusCode);                       // current tag: now the rules apply
    }

    [Fact]
    public async Task PUT_of_what_is_already_stored_is_200_with_the_same_etag_and_writes_no_event_or_audit_row()
    {
        var id = await RecordedAsync();
        var etag = await ETagOfAsync(id);
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_audit");
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_outbox");

        var response = await PutAsync(id, Correct(p: "rest and fluids"), etag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(etag, response.Headers.ETag?.ToString());
        Assert.Equal(0L, await Count("clinical_care_outbox"));
        Assert.Equal(0L, await Count("clinical_care_audit"));
    }

    [Fact]
    public async Task PUT_leaves_the_date_and_who_wrote_it_alone()
    {
        var id = await RecordedAsync();
        const string Identity = "SELECT CONCAT(`date`, '|', pid, '|', IFNULL(`user`, '')) FROM form_soap";
        var before = await fixture.RootScalarAsync<string>(Identity);

        await PutAsync(id, Correct(), await ETagOfAsync(id));

        Assert.Equal(before, await fixture.RootScalarAsync<string>(Identity));
    }

    [Fact]
    public async Task SEC_9_a_correction_leaves_one_success_row_naming_sections_and_each_refusal_one_failed_row()
    {
        var id = await RecordedAsync();
        var etag = await ETagOfAsync(id);
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_audit");

        await PutAsync(id, Correct(p: "secret detail"), etag);
        await PutAsync(id, Correct(), ContentVersion.Of(["stale"]).ToETag());
        await PutAsync(987_654, Correct(), etag);

        Assert.Equal(1L, await Count("clinical_care_audit", $"outcome = 'success' AND action = 'soap.correct' AND subject = 'soap:{id}'"));
        Assert.Equal(2L, await Count("clinical_care_audit", "outcome = 'failed' AND action = 'soap.correct'"));
        Assert.Equal("""["plan"]""", await fixture.RootScalarAsync<string>("SELECT details FROM clinical_care_audit WHERE action = 'soap.correct' AND outcome = 'success'"));
    }

    [Fact]
    public async Task Every_soap_route_is_401_without_the_gateway_secret()
    {
        using var anonymous = _factory.CreateClient();
        var id = await RecordedAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(Url(), Record())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Url(note: id))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Url())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PutAsync(id, Correct(), "\"c-00000000000000000000000000000000\"", client: anonymous)).StatusCode);
        Assert.Equal(1L, await Count("form_soap"));
    }
}
