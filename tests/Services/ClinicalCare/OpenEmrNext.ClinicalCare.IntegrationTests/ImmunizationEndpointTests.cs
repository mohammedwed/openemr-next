using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenEmrNext.ClinicalCare.Api.Contracts.Requests;
using OpenEmrNext.ClinicalCare.Api.Contracts.Responses;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;
using OpenEmrNext.ClinicalCare.Infrastructure;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// <c>/api/v1/immunizations</c> through the real HTTP pipeline, as the production principal (QLT-9): record, read, list and correct, with the update contract of ADR 0068 carried by a content version instead of a stamp
/// (ADR 0072): 200 with the new <c>ETag</c>, 428, 412, 400, 404 and 401.
/// </summary>
[Collection("ClinicalCareContainer")]
public class ImmunizationEndpointTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await ImmunizationTestData.SeedCodesAsync(fixture);
        await fixture.SeedPatientAsync(EncounterTestData.PatientUuid, EncounterTestData.PatientPid);
        _factory = ApiFactory.Create(fixture);
        _client = _factory.ClientWithSecret();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private static RecordImmunizationRequest Record(
        Guid? patient = null, int? vaccine = 6, string? lot = "L1", string? note = "arm sore", Guid? encounter = null, DateTime? given = null, decimal? amount = 0.5m,
        string? unit = "1", string? route = "intramuscular") =>
        new(patient ?? EncounterTestData.PatientUuid, given ?? new DateTime(2026, 11, 10, 8, 15, 30), vaccine, "140", "Acme", lot, new DateTime(2027, 1, 1), amount, unit, route,
            "left_arm", "Completed", "00", null, new DateTime(2026, 11, 10), new DateTime(2026, 8, 6), "Nurse Joy", 7, 9, encounter, "R1", "why", note, false, 7);

    private static CorrectImmunizationRequest Correct(string? note = "rechecked", string? lot = "L1", bool inError = false, int? vaccine = 6, Guid? encounter = null, decimal? amount = 0.5m, string? unit = "1") =>
        new(new DateTime(2026, 11, 10, 8, 15, 30), vaccine, "140", "Acme", lot, new DateTime(2027, 1, 1), amount, unit, "intramuscular", "left_arm", "Completed", "00", null,
            new DateTime(2026, 11, 10), new DateTime(2026, 8, 6), "Nurse Joy", 7, 9, encounter, "R1", "why", note, inError);

    private async Task<Guid> RecordedAsync(RecordImmunizationRequest? request = null)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/immunizations", request ?? Record());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ImmunizationRecordedResponse>())!.Uuid;
    }

    private async Task<string> ETagOfAsync(Guid uuid) => (await _client.GetAsync($"/api/v1/immunizations/{uuid}")).Headers.ETag!.ToString();

    private Task<HttpResponseMessage> PutAsync(Guid uuid, CorrectImmunizationRequest body, string? ifMatch = null, HttpClient? client = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/immunizations/{uuid}") { Content = JsonContent.Create(body) };
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
    public async Task POST_records_an_immunization_201_with_a_location_and_GET_reads_it_back_with_an_etag_that_matches_its_body()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/immunizations", Record());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ImmunizationRecordedResponse>();
        Assert.Equal($"/api/v1/immunizations/{created!.Uuid}", response.Headers.Location?.ToString());
        Assert.Equal(EncounterTestData.PatientUuid, created.PatientUuid);

        var read = await _client.GetAsync($"/api/v1/immunizations/{created.Uuid}");
        var body = await read.Content.ReadFromJsonAsync<ImmunizationResponse>();
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Matches("^\"c-[0-9a-f]{32}\"$", read.Headers.ETag!.ToString());
        Assert.Equal(read.Headers.ETag.ToString(), body!.ETag);
        Assert.Equal("arm sore", body.Note);
        Assert.Equal(6, body.ImmunizationId);
        Assert.Equal(0.5m, body.AmountAdministered);
        Assert.Equal(42, body.PatientPid);
    }

    [Fact]
    public async Task GET_of_an_unknown_immunization_is_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/immunizations/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task GET_list_returns_one_patients_immunizations_newest_first_each_with_the_etag_a_correction_needs()
    {
        await RecordedAsync(Record(given: new DateTime(2026, 1, 5)));
        var latest = await RecordedAsync(Record(given: new DateTime(2026, 6, 5)));

        var list = await _client.GetFromJsonAsync<ImmunizationListResponse>($"/api/v1/immunizations?patientUuid={EncounterTestData.PatientUuid}");

        Assert.Equal(2, list!.Total);
        Assert.Equal(latest, list.Items[0].Uuid);
        Assert.Equal(await ETagOfAsync(latest), list.Items[0].ETag);
        Assert.Equal(HttpStatusCode.OK, (await PutAsync(latest, Correct(), list.Items[0].ETag)).StatusCode);
    }

    [Fact]
    public async Task GET_list_needs_a_patient_a_registered_one_and_sane_bounds()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync("/api/v1/immunizations")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/immunizations?patientUuid={Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync($"/api/v1/immunizations?patientUuid={EncounterTestData.PatientUuid}&limit=100000")).StatusCode);
    }

    [Fact]
    public async Task POST_for_an_unknown_patient_or_encounter_is_404_and_records_nothing()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsJsonAsync("/api/v1/immunizations", Record(patient: Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsJsonAsync("/api/v1/immunizations", Record(encounter: Guid.NewGuid()))).StatusCode);

        Assert.Equal(0L, await Count("immunizations"));
        Assert.Equal(2L, await Count("clinical_care_audit", "outcome = 'failed' AND action = 'immunization.record'"));
    }

    [Fact]
    public async Task POST_with_a_linked_encounter_stores_its_number_and_reads_it_back()
    {
        var encounter = (await (await _client.PostAsJsonAsync(
                "/api/v1/encounters", new CreateEncounterRequest(EncounterTestData.PatientUuid, null, 5, null, "visit", 3, null, 7, null, null)))
            .Content.ReadFromJsonAsync<EncounterCreatedResponse>())!;

        var uuid = await RecordedAsync(Record(encounter: encounter.Uuid));

        var body = await _client.GetFromJsonAsync<ImmunizationResponse>($"/api/v1/immunizations/{uuid}");
        Assert.Equal(encounter.Number, body!.EncounterNumber);
        Assert.Equal(encounter.Uuid, body.EncounterUuid);   // so a correction can send the link back
    }

    [Fact]
    public async Task POST_with_invalid_fields_is_400_with_every_message_and_records_nothing()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/immunizations", Record(vaccine: 99, route: "nope", amount: 0m, unit: null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ErrorsOf(response);
        Assert.Contains("Immunization 99 is not a known immunization.", errors);
        Assert.Contains(errors, e => e.StartsWith("Route 'nope'", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("Amount administered must be", StringComparison.Ordinal));
        Assert.Equal(0L, await Count("immunizations"));
        Assert.Equal(1L, await Count("clinical_care_audit", "outcome = 'failed'"));
    }

    [Fact]
    public async Task POST_with_a_future_date_no_vaccine_or_a_bad_provider_is_400()
    {
        var noVaccine = Record(vaccine: null) with { CvxCode = null };

        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/v1/immunizations", Record(given: DateTime.Now.AddDays(30)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/v1/immunizations", noVaccine)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/v1/immunizations", Record() with { ProviderId = 0 })).StatusCode);
        Assert.Equal(0L, await Count("immunizations"));
    }

    [Fact]
    public async Task POST_accepts_an_already_expired_vaccine_because_giving_one_is_reportable_not_invalid()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/immunizations", Record() with { ExpirationDate = new DateTime(2025, 1, 1) });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task POST_with_a_note_of_multibyte_characters_over_the_column_is_400_not_a_database_error()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/immunizations", Record(note: new string('é', 40_000)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Note must be at most 65535 bytes.", await ErrorsOf(response));
    }

    [Fact]
    public async Task SEC_9_recording_leaves_one_success_row_and_one_event_and_no_clinical_content_in_either()
    {
        var uuid = await RecordedAsync(Record(note: "secret detail"));

        Assert.Equal(1L, await Count("clinical_care_audit", $"action = 'immunization.record' AND outcome = 'success' AND subject = '{uuid}'"));
        Assert.Equal(1L, await Count("clinical_care_outbox", "event_type = 'ImmunizationRecorded'"));
        Assert.DoesNotContain("secret", await fixture.RootScalarAsync<string>("SELECT payload FROM clinical_care_outbox"));
    }

    // Correct

    [Fact]
    public async Task PUT_with_the_current_etag_returns_200_the_new_state_and_a_new_etag_that_a_following_GET_agrees_with()
    {
        var uuid = await RecordedAsync();
        var etag = await ETagOfAsync(uuid);

        var response = await PutAsync(uuid, Correct(note: "rechecked", lot: "L2"), etag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ImmunizationResponse>();
        Assert.Equal("rechecked", body!.Note);
        Assert.Equal("L2", body.LotNumber);
        var newETag = response.Headers.ETag!.ToString();
        Assert.NotEqual(etag, newETag);
        Assert.Equal(newETag, body.ETag);
        Assert.Equal(newETag, await ETagOfAsync(uuid));
    }

    [Fact]
    public async Task A_correction_that_sends_the_encounter_back_keeps_the_link_and_one_that_leaves_it_out_clears_it()
    {
        var encounter = (await (await _client.PostAsJsonAsync(
                "/api/v1/encounters", new CreateEncounterRequest(EncounterTestData.PatientUuid, null, 5, null, "visit", 3, null, 7, null, null)))
            .Content.ReadFromJsonAsync<EncounterCreatedResponse>())!;
        var uuid = await RecordedAsync(Record(encounter: encounter.Uuid));

        var kept = await PutAsync(uuid, Correct(encounter: encounter.Uuid), await ETagOfAsync(uuid));
        var cleared = await PutAsync(uuid, Correct(note: "again"), kept.Headers.ETag!.ToString());

        var clearedBody = await cleared.Content.ReadFromJsonAsync<ImmunizationResponse>();
        Assert.Equal(encounter.Uuid, (await kept.Content.ReadFromJsonAsync<ImmunizationResponse>())!.EncounterUuid);
        Assert.Null(clearedBody!.EncounterUuid);   // replace-the-whole-set: leaving it out removes it
        Assert.Null(clearedBody.EncounterNumber);
    }

    [Fact]
    public async Task PUT_can_clear_a_field_by_leaving_it_out_and_mark_an_entry_in_error_and_back()
    {
        var uuid = await RecordedAsync();

        var cleared = await PutAsync(uuid, Correct(note: null, amount: null, unit: null, inError: true), await ETagOfAsync(uuid));
        var body = await cleared.Content.ReadFromJsonAsync<ImmunizationResponse>();

        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        Assert.Null(body!.Note);
        Assert.Null(body.AmountAdministered);
        Assert.True(body.EnteredInError);
        var restored = await PutAsync(uuid, Correct(), cleared.Headers.ETag!.ToString());
        Assert.False((await restored.Content.ReadFromJsonAsync<ImmunizationResponse>())!.EnteredInError);
    }

    [Fact]
    public async Task PUT_with_no_if_match_a_wildcard_or_a_blank_one_is_428_and_writes_nothing()
    {
        var uuid = await RecordedAsync();

        foreach (var header in new string?[] { null, "*", "", "   " })
            Assert.Equal((HttpStatusCode)428, (await PutAsync(uuid, Correct(), header)).StatusCode);

        Assert.Equal("arm sore", await fixture.RootScalarAsync<string>("SELECT note FROM immunizations"));
    }

    [Fact]
    public async Task PUT_with_a_stale_unreadable_weak_or_stamp_shaped_etag_is_412_and_the_stale_one_carries_the_current_etag()
    {
        var uuid = await RecordedAsync();
        var current = await ETagOfAsync(uuid);
        var stale = ContentVersion.Of(["stale"]).ToETag();

        var response = await PutAsync(uuid, Correct(), stale);

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        Assert.Equal(current, response.Headers.ETag?.ToString());
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await PutAsync(uuid, Correct(), "garbage")).StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await PutAsync(uuid, Correct(), "W/" + current)).StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await PutAsync(uuid, Correct(), ConcurrencyToken.From(DateTime.Now).ToETag())).StatusCode);
        Assert.Equal("arm sore", await fixture.RootScalarAsync<string>("SELECT note FROM immunizations"));
        Assert.Equal(0L, await Count("clinical_care_outbox", "event_type = 'ImmunizationCorrected'"));
    }

    [Fact]
    public async Task LEG_10_the_legacy_screen_replacing_the_row_after_the_read_makes_the_correction_a_412_though_no_stamp_moved()
    {
        var uuid = await RecordedAsync();
        var readTag = await ETagOfAsync(uuid);
        // What the issue screen's REPLACE INTO does to the row: rewrite it without touching update_date.
        await fixture.RootExecuteAsync("UPDATE immunizations SET lot_number = 'LEGACY', update_date = update_date");

        var response = await PutAsync(uuid, Correct(), readTag);

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        Assert.Equal("LEGACY", await fixture.RootScalarAsync<string>("SELECT lot_number FROM immunizations"));
        Assert.NotEqual(readTag, response.Headers.ETag?.ToString());
    }

    [Fact]
    public async Task PUT_for_an_unknown_immunization_or_encounter_is_404()
    {
        var uuid = await RecordedAsync();
        var etag = await ETagOfAsync(uuid);

        Assert.Equal(HttpStatusCode.NotFound, (await PutAsync(Guid.NewGuid(), Correct(), etag)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PutAsync(uuid, Correct(encounter: Guid.NewGuid()), etag)).StatusCode);
    }

    [Fact]
    public async Task PUT_with_invalid_fields_is_400_with_every_message_and_writes_nothing()
    {
        var uuid = await RecordedAsync();

        var response = await PutAsync(uuid, Correct(vaccine: 99, amount: 0m), await ETagOfAsync(uuid));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ErrorsOf(response);
        Assert.Contains("Immunization 99 is not a known immunization.", errors);
        Assert.Contains(errors, e => e.StartsWith("Amount administered must be", StringComparison.Ordinal));
        Assert.Equal("arm sore", await fixture.RootScalarAsync<string>("SELECT note FROM immunizations"));
    }

    [Fact]
    public async Task The_order_of_checks_is_a_malformed_body_then_428_then_404_then_412_then_domain_rules()
    {
        var uuid = await RecordedAsync();
        var stale = ContentVersion.Of(["stale"]).ToETag();
        var badBody = Correct() with { CvxCode = new string('1', 99) };

        Assert.Equal(HttpStatusCode.BadRequest, (await PutAsync(uuid, badBody)).StatusCode);                                        // binding, and no If-Match
        Assert.Equal((HttpStatusCode)428, (await PutAsync(uuid, Correct(vaccine: 99))).StatusCode);                                 // domain-invalid but no precondition
        Assert.Equal(HttpStatusCode.NotFound, (await PutAsync(Guid.NewGuid(), Correct(vaccine: 99), stale)).StatusCode);          // unknown row before a stale tag
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await PutAsync(uuid, Correct(vaccine: 99), stale)).StatusCode);          // stale before domain rules
    }

    [Fact]
    public async Task PUT_of_what_is_already_stored_is_200_with_the_same_etag_and_writes_no_event_or_audit_row()
    {
        var uuid = await RecordedAsync();
        var etag = await ETagOfAsync(uuid);
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_audit");
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_outbox");

        var response = await PutAsync(uuid, Correct(note: "arm sore"), etag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(etag, response.Headers.ETag?.ToString());
        Assert.Equal(0L, await Count("clinical_care_outbox"));
        Assert.Equal(0L, await Count("clinical_care_audit"));
    }

    [Fact]
    public async Task SEC_9_a_correction_leaves_one_success_row_naming_fields_and_no_values_and_each_refusal_one_failed_row()
    {
        var uuid = await RecordedAsync();
        var etag = await ETagOfAsync(uuid);
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_audit");

        await PutAsync(uuid, Correct(note: "secret detail"), etag);
        await PutAsync(uuid, Correct(), ContentVersion.Of(["stale"]).ToETag());
        await PutAsync(Guid.NewGuid(), Correct(), etag);

        Assert.Equal(1L, await Count("clinical_care_audit", "outcome = 'success' AND action = 'immunization.correct'"));
        Assert.Equal(2L, await Count("clinical_care_audit", "outcome = 'failed' AND action = 'immunization.correct'"));
        var details = await fixture.RootScalarAsync<string>("SELECT details FROM clinical_care_audit WHERE outcome = 'success'");
        Assert.Equal("""["note"]""", details);
    }

    [Fact]
    public async Task Every_immunization_route_is_401_without_the_gateway_secret()
    {
        using var anonymous = _factory.CreateClient();
        var uuid = await RecordedAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/v1/immunizations", Record())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/v1/immunizations/{uuid}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/v1/immunizations?patientUuid={EncounterTestData.PatientUuid}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PutAsync(uuid, Correct(), "\"c-00000000000000000000000000000000\"", anonymous)).StatusCode);
        Assert.Equal(1L, await Count("immunizations"));
    }
}
