using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenEmrNext.ClinicalCare.Api.Contracts.Requests;
using OpenEmrNext.ClinicalCare.Api.Contracts.Responses;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// <c>/api/v1/encounters/{encounterUuid}/clinical-notes</c> and the note lookups through the real HTTP pipeline, as the production principal (QLT-9, ADR 0074): record, read, list and correct, with the update contract
/// of ADR 0068 (200 with the new <c>ETag</c>, 428, 412, 400, 404, 401), and a note reachable only through its own encounter.
/// </summary>
[Collection("ClinicalCareContainer")]
public class ClinicalNoteEndpointTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    private static readonly string StampETag = ConcurrencyToken.From(ClinicalNoteTestData.Stamp).ToETag();

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private Guid _encounter;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await ClinicalNoteTestData.SeedCodesAsync(fixture);
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

    private string Url(Guid? encounter = null, Guid? note = null) => $"/api/v1/encounters/{encounter ?? _encounter}/clinical-notes" + (note is null ? "" : $"/{note}");

    private static RecordClinicalNoteRequest Record(string? type = "progress_note", string? category = "cardiology", string? text = "Patient comfortable.", DateTime? date = null, long provider = 7) =>
        new(type, category, text, date ?? new DateTime(2026, 11, 10), provider);

    private static CorrectClinicalNoteRequest Correct(string? type = "progress_note", string? category = "cardiology", string? text = "Revised: comfortable, eating.", DateTime? date = null) =>
        new(type, category, text, date ?? new DateTime(2026, 11, 10));

    private async Task<Guid> RecordedAsync(RecordClinicalNoteRequest? request = null, Guid? encounter = null)
    {
        var response = await _client.PostAsJsonAsync(Url(encounter), request ?? Record());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ClinicalNoteRecordedResponse>())!.Uuid;
    }

    /// <summary>Gives the note a known stamp (an explicit value stops the database moving it), so a correction has a version to be based on.</summary>
    private Task PinStampAsync() => fixture.RootExecuteAsync($"UPDATE form_clinical_notes SET last_updated = '{ClinicalNoteTestData.Stamp:yyyy-MM-dd HH:mm:ss}'");

    private Task<HttpResponseMessage> PutAsync(Guid note, CorrectClinicalNoteRequest body, string? ifMatch = null, Guid? encounter = null, HttpClient? client = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, Url(encounter, note)) { Content = JsonContent.Create(body) };
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

    // Record, get, list

    [Fact]
    public async Task POST_records_a_note_201_with_a_location_and_GET_reads_it_back_with_the_derived_code_and_an_etag_matching_its_body()
    {
        var response = await _client.PostAsJsonAsync(Url(), Record());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ClinicalNoteRecordedResponse>();
        Assert.Equal($"/api/v1/encounters/{_encounter}/clinical-notes/{created!.Uuid}", response.Headers.Location?.ToString());

        var read = await _client.GetAsync(Url(note: created.Uuid));
        var body = await read.Content.ReadFromJsonAsync<ClinicalNoteResponse>();
        Assert.Equal(read.Headers.ETag!.ToString(), body!.ETag);
        Assert.Equal("LOINC:11506-3", body.Code);
        Assert.Equal("Progress Note", body.CodeText);
        Assert.Equal("dr_who", body.RecordedBy);
        Assert.Equal("Patient comfortable.", body.Description);
    }

    [Fact]
    public async Task POST_with_no_date_uses_today()
    {
        var response = await _client.PostAsJsonAsync(Url(), Record() with { Date = null });
        var uuid = (await response.Content.ReadFromJsonAsync<ClinicalNoteRecordedResponse>())!.Uuid;

        var body = await _client.GetFromJsonAsync<ClinicalNoteResponse>(Url(note: uuid));

        Assert.Equal(DateTime.Now.Date, body!.Date!.Value.Date);
    }

    [Fact]
    public async Task GET_of_an_unknown_note_or_encounter_is_404_and_a_note_is_not_reachable_through_another_encounter()
    {
        var uuid = await RecordedAsync();
        var other = await NewEncounterAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(Url(note: Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(Url(Guid.NewGuid(), uuid))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(Url(other, uuid))).StatusCode);
    }

    [Fact]
    public async Task GET_list_returns_the_encounters_notes_newest_first_each_with_the_etag_a_correction_needs()
    {
        await RecordedAsync(Record(text: "first", date: new DateTime(2026, 11, 1)));
        var latest = await RecordedAsync(Record(text: "second", date: new DateTime(2026, 11, 5)));
        await RecordedAsync(Record(text: "elsewhere"), await NewEncounterAsync());
        await PinStampAsync();

        var list = await _client.GetFromJsonAsync<ClinicalNoteListResponse>(Url());

        Assert.Equal(2, list!.Total);
        Assert.Equal(latest, list.Items[0].Uuid);
        Assert.Equal(StampETag, list.Items[0].ETag);
        Assert.Equal(HttpStatusCode.OK, (await PutAsync(latest, Correct(text: "second"), list.Items[0].ETag)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/encounters/{Guid.NewGuid()}/clinical-notes")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync(Url() + "?limit=100000")).StatusCode);
    }

    [Fact]
    public async Task POST_with_bad_fields_is_400_with_every_message_and_records_nothing()
    {
        var response = await _client.PostAsJsonAsync(Url(), Record(type: "nonsense", category: "nope", text: "x", date: new DateTime(1800, 1, 1)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ErrorsOf(response);
        Assert.Contains("Type 'nonsense' is not a known note type.", errors);
        Assert.Contains("Category 'nope' is not a known note category.", errors);
        Assert.Contains("Description must be at least 2 characters.", errors);
        Assert.Contains(errors, e => e.StartsWith("Date must be between", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync(Url(), Record(type: null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync(Url(), Record(provider: 0))).StatusCode);
        Assert.Equal(0L, await Count("form_clinical_notes"));
        Assert.Equal(0L, await Count("forms", "formdir = 'clinical_notes'"));
    }

    [Fact]
    public async Task POST_with_a_multibyte_note_over_the_column_is_400_not_a_database_error_and_an_empty_one_is_refused()
    {
        var big = await _client.PostAsJsonAsync(Url(), Record(text: new string('é', 40_000)));
        var empty = await _client.PostAsJsonAsync(Url(), Record(text: "   "));

        Assert.Contains("Description must be at most 65535 bytes.", await ErrorsOf(big));
        Assert.Contains("A note is required.", await ErrorsOf(empty));
    }

    [Fact]
    public async Task POST_for_an_unknown_encounter_is_404_and_audited()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsJsonAsync(Url(Guid.NewGuid()), Record())).StatusCode);

        Assert.Equal(0L, await Count("form_clinical_notes"));
        Assert.Equal(1L, await Count("clinical_care_audit", "outcome = 'failed' AND action = 'clinical_note.record'"));
    }

    [Fact]
    public async Task SEC_9_recording_leaves_one_success_row_naming_the_note_and_no_text_and_one_event()
    {
        var uuid = await RecordedAsync(Record(text: "secret finding"));

        Assert.Equal(1L, await Count("clinical_care_audit", $"action = 'clinical_note.record' AND outcome = 'success' AND subject = '{uuid}'"));
        Assert.DoesNotContain("secret", await fixture.RootScalarAsync<string>("SELECT payload FROM clinical_care_outbox WHERE event_type = 'ClinicalNoteRecorded'"));
    }

    // Correct

    [Fact]
    public async Task PUT_with_the_current_etag_returns_200_the_new_state_and_a_new_etag_that_a_following_GET_agrees_with()
    {
        var uuid = await RecordedAsync();
        await PinStampAsync();

        var response = await PutAsync(uuid, Correct(type: "nurse_note"), StampETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ClinicalNoteResponse>();
        Assert.Equal("nurse_note", body!.Type);
        Assert.Equal("LOINC:34746-8", body.Code);   // the code follows the type
        Assert.Equal("Revised: comfortable, eating.", body.Description);
        var newETag = response.Headers.ETag!.ToString();
        Assert.NotEqual(StampETag, newETag);
        Assert.Equal(newETag, body.ETag);
        Assert.Equal(newETag, (await _client.GetAsync(Url(note: uuid))).Headers.ETag!.ToString());
    }

    [Fact]
    public async Task PUT_can_clear_the_category_by_leaving_it_out()
    {
        var uuid = await RecordedAsync();
        await PinStampAsync();

        var response = await PutAsync(uuid, Correct(category: null), StampETag);

        Assert.Null((await response.Content.ReadFromJsonAsync<ClinicalNoteResponse>())!.Category);
    }

    [Fact]
    public async Task PUT_with_no_if_match_a_wildcard_or_a_blank_one_is_428_and_writes_nothing()
    {
        var uuid = await RecordedAsync();

        foreach (var header in new string?[] { null, "*", "", "   " })
            Assert.Equal((HttpStatusCode)428, (await PutAsync(uuid, Correct(), header)).StatusCode);

        Assert.Equal("Patient comfortable.", await fixture.RootScalarAsync<string>("SELECT description FROM form_clinical_notes"));
    }

    [Fact]
    public async Task PUT_with_a_stale_unreadable_or_weak_etag_is_412_and_the_stale_one_carries_the_current_etag()
    {
        var uuid = await RecordedAsync();
        await PinStampAsync();

        var stale = await PutAsync(uuid, Correct(), ConcurrencyToken.From(ClinicalNoteTestData.Stamp.AddDays(-5)).ToETag());

        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal(StampETag, stale.Headers.ETag?.ToString());
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await PutAsync(uuid, Correct(), "garbage")).StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await PutAsync(uuid, Correct(), "W/" + StampETag)).StatusCode);
        Assert.Equal("Patient comfortable.", await fixture.RootScalarAsync<string>("SELECT description FROM form_clinical_notes"));
    }

    [Fact]
    public async Task LEG_10_a_legacy_edit_after_the_read_makes_the_correction_a_412_and_the_other_writers_text_survives()
    {
        var uuid = await RecordedAsync();
        await PinStampAsync();
        await fixture.RootExecuteAsync("UPDATE form_clinical_notes SET description = 'edited in the legacy screen'");

        var response = await PutAsync(uuid, Correct(), StampETag);

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        Assert.Equal("edited in the legacy screen", await fixture.RootScalarAsync<string>("SELECT description FROM form_clinical_notes"));
        Assert.NotEqual(StampETag, response.Headers.ETag?.ToString());
    }

    [Fact]
    public async Task PUT_through_another_encounter_for_an_unknown_note_or_a_retired_one_is_404_and_changes_nothing()
    {
        var uuid = await RecordedAsync();
        await PinStampAsync();
        var other = await NewEncounterAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await PutAsync(uuid, Correct(), StampETag, other)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PutAsync(Guid.NewGuid(), Correct(), StampETag)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PutAsync(uuid, Correct(), StampETag, Guid.NewGuid())).StatusCode);
        await fixture.RootExecuteAsync($"UPDATE form_clinical_notes SET activity = 0, last_updated = '{ClinicalNoteTestData.Stamp:yyyy-MM-dd HH:mm:ss}'");
        Assert.Equal(HttpStatusCode.NotFound, (await PutAsync(uuid, Correct(), StampETag)).StatusCode);
        Assert.Equal("Patient comfortable.", await fixture.RootScalarAsync<string>("SELECT description FROM form_clinical_notes"));
    }

    [Fact]
    public async Task PUT_with_bad_fields_is_400_and_the_order_of_checks_is_a_malformed_body_then_428_then_404_then_412_then_the_rules()
    {
        var uuid = await RecordedAsync();
        await PinStampAsync();
        var stale = ConcurrencyToken.From(ClinicalNoteTestData.Stamp.AddDays(-5)).ToETag();

        Assert.Equal(HttpStatusCode.BadRequest, (await PutAsync(uuid, Correct(type: null))).StatusCode);                                  // binding, and no If-Match
        Assert.Equal((HttpStatusCode)428, (await PutAsync(uuid, Correct(type: "nope"))).StatusCode);                                      // invalid but no precondition
        Assert.Equal(HttpStatusCode.NotFound, (await PutAsync(Guid.NewGuid(), Correct(type: "nope"), stale)).StatusCode);                 // unknown note before a stale tag
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await PutAsync(uuid, Correct(type: "nope"), stale)).StatusCode);                 // stale before the rules
        var invalid = await PutAsync(uuid, Correct(type: "nope", text: "x"), StampETag);                                                  // current tag: now the rules apply
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("Type 'nope' is not a known note type.", await ErrorsOf(invalid));
        Assert.Equal("Patient comfortable.", await fixture.RootScalarAsync<string>("SELECT description FROM form_clinical_notes"));
    }

    [Fact]
    public async Task PUT_of_what_is_already_stored_is_200_with_the_same_etag_and_writes_no_event_or_audit_row()
    {
        var uuid = await RecordedAsync();
        await PinStampAsync();
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_audit");
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_outbox");

        var response = await PutAsync(uuid, Correct(text: "Patient comfortable."), StampETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(StampETag, response.Headers.ETag?.ToString());
        Assert.Equal(0L, await Count("clinical_care_outbox"));
        Assert.Equal(0L, await Count("clinical_care_audit"));
    }

    [Fact]
    public async Task PUT_leaves_who_wrote_the_note_and_its_parent_form_alone()
    {
        var uuid = await RecordedAsync();
        await PinStampAsync();
        const string Identity = "SELECT CONCAT(form_id, '|', IFNULL(`user`, ''), '|', IFNULL(groupname, ''), '|', activity, '|', authorized) FROM form_clinical_notes";
        var before = await fixture.RootScalarAsync<string>(Identity);

        await PutAsync(uuid, Correct(), StampETag);

        Assert.Equal(before, await fixture.RootScalarAsync<string>(Identity));
    }

    [Fact]
    public async Task SEC_9_a_correction_leaves_one_success_row_naming_fields_and_each_refusal_one_failed_row()
    {
        var uuid = await RecordedAsync();
        await PinStampAsync();
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_audit");

        await PutAsync(uuid, Correct(text: "secret detail"), StampETag);
        await PutAsync(uuid, Correct(), ConcurrencyToken.From(ClinicalNoteTestData.Stamp.AddDays(-5)).ToETag());
        await PutAsync(Guid.NewGuid(), Correct(), StampETag);

        Assert.Equal(1L, await Count("clinical_care_audit", $"outcome = 'success' AND action = 'clinical_note.correct' AND subject = '{uuid}'"));
        Assert.Equal(2L, await Count("clinical_care_audit", "outcome = 'failed' AND action = 'clinical_note.correct'"));
        Assert.Equal("""["description"]""", await fixture.RootScalarAsync<string>("SELECT details FROM clinical_care_audit WHERE outcome = 'success'"));
    }

    // Lookups and authentication

    [Fact]
    public async Task The_note_lookups_offer_the_active_types_and_categories_in_legacys_order_and_what_they_offer_is_accepted()
    {
        var types = await _client.GetFromJsonAsync<List<ClinicalNoteTypeLookupResponse>>("/api/v1/lookups/clinical-note-types");
        var categories = await _client.GetFromJsonAsync<List<ClinicalNoteCategoryLookupResponse>>("/api/v1/lookups/clinical-note-categories");

        Assert.NotNull(types);
        Assert.Equal(["progress_note", "nurse_note"], types.Select(t => t.Id));   // the retired one is not offered
        Assert.Equal("LOINC:11506-3", types[0].Code);
        Assert.NotNull(categories);
        Assert.Equal(["cardiology"], categories.Select(c => c.Id));
        Assert.Equal(HttpStatusCode.Created, (await _client.PostAsJsonAsync(Url(), Record(type: types[1].Id, category: categories![0].Id))).StatusCode);
    }

    [Fact]
    public async Task Every_clinical_note_route_is_401_without_the_gateway_secret()
    {
        using var anonymous = _factory.CreateClient();
        var uuid = await RecordedAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(Url(), Record())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Url(note: uuid))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Url())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PutAsync(uuid, Correct(), StampETag, client: anonymous)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/lookups/clinical-note-types")).StatusCode);
        Assert.Equal(1L, await Count("form_clinical_notes"));
    }
}
