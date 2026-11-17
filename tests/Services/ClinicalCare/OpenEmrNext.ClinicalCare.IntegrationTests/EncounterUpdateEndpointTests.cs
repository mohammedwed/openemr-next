using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenEmrNext.ClinicalCare.Api.Contracts.Requests;
using OpenEmrNext.ClinicalCare.Api.Contracts.Responses;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// <c>PUT /api/v1/encounters/{uuid}</c> through the real HTTP pipeline (auth, binding, validation, use case, adapters, database), as the production
/// principal (QLT-9). The contract under test: 200 with the new <c>ETag</c>; 428 with no <c>If-Match</c>; 412 when the record moved on, including
/// when another writer changed it; 400 for invalid input; 404 for a record that is not there (ADR 0068, API-3).
/// </summary>
[Collection("ClinicalCareContainer")]
public class EncounterUpdateEndpointTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
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
        _factory = ApiFactory.Create(fixture);
        _client = _factory.ClientWithSecret();

        var created = await (await _client.PostAsJsonAsync(
                "/api/v1/encounters",
                new CreateEncounterRequest(EncounterTestData.PatientUuid, "AMB", 5, new DateTime(2026, 11, 10, 8, 0, 0), "Cough", 3, null, 7, null, null)))
            .Content.ReadFromJsonAsync<EncounterCreatedResponse>();
        _uuid = created!.Uuid;

        // A known stamp in the past, so "the tag changed" is decided by the update and not by what second the test ran in; and a clean audit
        // and outbox so each test counts only its own rows.
        await fixture.RootExecuteAsync($"UPDATE form_encounter SET last_update = '{Stamp:yyyy-MM-dd HH:mm:ss}'");
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_audit");
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_outbox");
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private static UpdateEncounterRequest Request(
        string? classCode = "EMER", int category = 9, DateTime? date = null, string? reason = "Chest pain", int facilityId = 3, int? pos = 21,
        long providerId = 12) =>
        new(classCode, category, date ?? new DateTime(2026, 11, 12, 14, 0, 0), new DateTime(2026, 11, 1), reason, facilityId, pos, providerId);

    private Task<HttpResponseMessage> PutAsync(UpdateEncounterRequest body, string? ifMatch = null, Guid? uuid = null, HttpClient? client = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/encounters/{uuid ?? _uuid}") { Content = JsonContent.Create(body) };
        if (ifMatch is not null)
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        return (client ?? _client).SendAsync(request);
    }

    private Task<long> Count(string table, string where = "1=1") => fixture.RootScalarAsync<long>($"SELECT COUNT(*) FROM {table} WHERE {where}");

    private static async Task<string[]> ErrorsOf(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("errors").EnumerateObject()
            .SelectMany(p => p.Value.EnumerateArray().Select(e => e.GetString()!))
            .ToArray();
    }

    [Fact]
    public async Task PUT_with_the_current_etag_returns_200_the_new_state_and_a_new_etag_that_a_following_GET_agrees_with()
    {
        var response = await PutAsync(Request(), StampETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<EncounterResponse>();
        Assert.Equal(_uuid, body!.Uuid);
        Assert.Equal("EMER", body.ClassCode);
        Assert.Equal("Chest pain", body.Reason);
        Assert.Equal(9, body.CalendarCategoryId);
        Assert.Equal(12, body.ProviderId);
        Assert.Equal(21, body.PosCode);
        Assert.Equal(new DateTime(2026, 11, 12, 14, 0, 0), body.Date);
        Assert.Equal("Main Clinic", body.FacilityName);

        var newETag = response.Headers.ETag?.ToString();
        Assert.NotNull(newETag);
        Assert.NotEqual(StampETag, newETag);
        Assert.Equal(newETag, (await _client.GetAsync($"/api/v1/encounters/{_uuid}")).Headers.ETag?.ToString());
    }

    [Fact]
    public async Task The_new_etag_can_be_used_for_the_next_edit_without_another_read()
    {
        var first = await PutAsync(Request(), StampETag);
        // The next edit is within the same second as the first, so the database stamp cannot have moved: the tag is the same and still current.
        await fixture.RootExecuteAsync("UPDATE form_encounter SET last_update = '2026-03-04 05:06:07'");
        var current = ConcurrencyToken.From(new DateTime(2026, 3, 4, 5, 6, 7)).ToETag();

        var second = await PutAsync(Request(reason: "Second edit"), current);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal("Second edit", await fixture.RootScalarAsync<string>("SELECT reason FROM form_encounter"));
    }

    [Fact]
    public async Task PUT_with_no_if_match_is_428_and_writes_nothing()
    {
        var response = await PutAsync(Request());

        Assert.Equal((HttpStatusCode)428, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Cough", await fixture.RootScalarAsync<string>("SELECT reason FROM form_encounter"));
        Assert.Equal(0L, await Count("clinical_care_audit"));
    }

    [Theory]
    [InlineData("*")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PUT_with_a_wildcard_or_blank_if_match_is_428_because_that_would_switch_the_protection_off(string ifMatch)
    {
        var response = await PutAsync(Request(), ifMatch);

        Assert.Equal((HttpStatusCode)428, response.StatusCode);
        Assert.Equal("Cough", await fixture.RootScalarAsync<string>("SELECT reason FROM form_encounter"));
    }

    [Fact]
    public async Task PUT_with_a_stale_etag_is_412_carries_the_current_etag_and_writes_nothing()
    {
        var stale = ConcurrencyToken.From(Stamp.AddDays(-5)).ToETag();

        var response = await PutAsync(Request(), stale);

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(StampETag, response.Headers.ETag?.ToString());   // so the caller can reload and try again
        Assert.Equal("Cough", await fixture.RootScalarAsync<string>("SELECT reason FROM form_encounter"));
        Assert.Equal(0L, await Count("clinical_care_outbox"));
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("\"nonsense\"")]
    [InlineData("W/\"1\"")]
    public async Task PUT_with_an_unreadable_or_weak_tag_is_412_not_428_and_not_a_success(string ifMatch)
    {
        var response = await PutAsync(Request(), ifMatch);

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        Assert.Equal("Cough", await fixture.RootScalarAsync<string>("SELECT reason FROM form_encounter"));
    }

    [Fact]
    public async Task PUT_with_a_weak_form_of_the_correct_tag_is_still_412_because_if_match_compares_strongly()
    {
        var response = await PutAsync(Request(), "W/" + StampETag);

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
    }

    [Fact]
    public async Task LEG_10_a_legacy_edit_after_the_read_makes_the_update_a_412_and_the_other_writers_value_survives()
    {
        var read = await _client.GetAsync($"/api/v1/encounters/{_uuid}");
        var etagReadBefore = read.Headers.ETag!.ToString();
        await fixture.RootExecuteAsync("UPDATE form_encounter SET reason = 'edited in the legacy screen'");   // the database moves last_update

        var response = await PutAsync(Request(), etagReadBefore);

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        Assert.Equal("edited in the legacy screen", await fixture.RootScalarAsync<string>("SELECT reason FROM form_encounter"));
        Assert.NotEqual(etagReadBefore, response.Headers.ETag?.ToString());
    }

    [Fact]
    public async Task PUT_applies_if_any_one_of_several_offered_tags_is_current()
    {
        var stale = ConcurrencyToken.From(Stamp.AddDays(-5)).ToETag();

        var response = await PutAsync(Request(), $"{stale}, {StampETag}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PUT_for_an_unknown_encounter_is_404_problem_json()
    {
        var response = await PutAsync(Request(), StampETag, uuid: Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task PUT_for_an_encounter_legacy_has_deleted_is_404_and_changes_nothing()
    {
        await fixture.RootExecuteAsync("UPDATE forms SET deleted = 1 WHERE formdir = 'newpatient'");

        var response = await PutAsync(Request(), StampETag);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Cough", await fixture.RootScalarAsync<string>("SELECT reason FROM form_encounter"));
    }

    [Fact]
    public async Task PUT_with_an_unknown_class_is_400_with_the_domains_message_and_writes_nothing()
    {
        var response = await PutAsync(Request(classCode: "NOPE"), StampETag);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Class 'NOPE' is not a known encounter class.", await ErrorsOf(response));
        Assert.Equal("Cough", await fixture.RootScalarAsync<string>("SELECT reason FROM form_encounter"));
    }

    [Fact]
    public async Task PUT_with_an_unknown_facility_is_400()
    {
        var response = await PutAsync(Request(facilityId: 99), StampETag);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Facility 99 does not exist.", await ErrorsOf(response));
    }

    [Fact]
    public async Task PUT_with_no_class_or_no_date_is_400_because_an_update_never_fills_a_field_in()
    {
        var noClass = await PutAsync(Request(classCode: null), StampETag);
        var noDate = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Put, $"/api/v1/encounters/{_uuid}")
        {
            Content = JsonContent.Create(new { classCode = "AMB", calendarCategoryId = 5, facilityId = 3, providerId = 7 }),
            Headers = { { "If-Match", StampETag } }
        });

        Assert.Equal(HttpStatusCode.BadRequest, noClass.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noDate.StatusCode);
        Assert.Equal("Cough", await fixture.RootScalarAsync<string>("SELECT reason FROM form_encounter"));
    }

    [Fact]
    public async Task A_malformed_body_is_a_400_at_the_binding_boundary_even_with_no_if_match_but_a_domain_rule_failure_is_not()
    {
        var malformed = await PutAsync(Request(classCode: null));                  // no If-Match, and the class is missing
        var domainRule = await PutAsync(Request(classCode: "NOPE"));               // no If-Match, and the class is unknown

        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);              // refused before the handler runs
        Assert.Equal((HttpStatusCode)428, domainRule.StatusCode);                   // the precondition is asked for before the domain judges the body
    }

    [Fact]
    public async Task A_stale_tag_with_a_domain_rule_failure_is_412_because_the_precondition_is_judged_first()
    {
        var response = await PutAsync(Request(classCode: "NOPE"), ConcurrencyToken.From(Stamp.AddDays(-5)).ToETag());

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
    }

    [Fact]
    public async Task PUT_with_a_provider_outside_the_column_range_is_400_not_a_500()
    {
        var response = await PutAsync(Request(providerId: 3_000_000_000), StampETag);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PUT_of_what_is_already_stored_is_200_with_the_same_etag_and_writes_no_event_or_audit_row()
    {
        var stored = await (await _client.GetAsync($"/api/v1/encounters/{_uuid}")).Content.ReadFromJsonAsync<EncounterResponse>();
        var same = new UpdateEncounterRequest(
            stored!.ClassCode, stored.CalendarCategoryId!.Value, stored.Date, stored.OnsetDate, stored.Reason, stored.FacilityId, stored.PosCode, stored.ProviderId);

        var response = await PutAsync(same, StampETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(StampETag, response.Headers.ETag?.ToString());
        Assert.Equal(0L, await Count("clinical_care_outbox"));
        Assert.Equal(0L, await Count("clinical_care_audit"));
    }

    [Fact]
    public async Task SEC_9_a_successful_update_leaves_one_success_row_naming_fields_and_no_values_and_one_event()
    {
        var response = await PutAsync(Request(reason: "Chest pain"), StampETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1L, await Count("clinical_care_audit"));
        Assert.Equal("encounter.update", await fixture.RootScalarAsync<string>("SELECT action FROM clinical_care_audit"));
        Assert.Equal("success", await fixture.RootScalarAsync<string>("SELECT outcome FROM clinical_care_audit"));
        Assert.Equal(_uuid.ToString(), await fixture.RootScalarAsync<string>("SELECT CONCAT(subject, '') FROM clinical_care_audit"));
        var details = await fixture.RootScalarAsync<string>("SELECT details FROM clinical_care_audit");
        Assert.Contains("reason", details);
        Assert.DoesNotContain("Chest pain", details);
        Assert.Equal(1L, await Count("clinical_care_outbox", "event_type = 'EncounterUpdated'"));
        Assert.False(string.IsNullOrWhiteSpace(await fixture.RootScalarAsync<string>("SELECT correlation_id FROM clinical_care_audit")));
    }

    [Fact]
    public async Task SEC_9_a_412_and_a_404_each_leave_one_failed_row()
    {
        await PutAsync(Request(), ConcurrencyToken.From(Stamp.AddDays(-5)).ToETag());
        await PutAsync(Request(), StampETag, uuid: Guid.NewGuid());

        Assert.Equal(2L, await Count("clinical_care_audit", "outcome = 'failed' AND action = 'encounter.update'"));
        Assert.Equal(0L, await Count("clinical_care_audit", "outcome = 'success'"));
    }

    [Fact]
    public async Task A_428_is_refused_before_the_business_layer_and_so_is_not_audited()
    {
        await PutAsync(Request());

        Assert.Equal(0L, await Count("clinical_care_audit"));
    }

    [Fact]
    public async Task PUT_leaves_sensitivity_and_billing_columns_as_they_were()
    {
        await fixture.RootExecuteAsync(
            $"UPDATE form_encounter SET sensitivity = 'high', billing_note = 'billed', in_collection = 1, last_update = '{Stamp:yyyy-MM-dd HH:mm:ss}'");

        await PutAsync(Request(), StampETag);

        Assert.Equal("high", await fixture.RootScalarAsync<string>("SELECT sensitivity FROM form_encounter"));
        Assert.Equal("billed", await fixture.RootScalarAsync<string>("SELECT billing_note FROM form_encounter"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT in_collection FROM form_encounter"));
    }

    [Fact]
    public async Task PUT_with_no_gateway_secret_is_401_and_writes_nothing()
    {
        using var anonymous = _factory.CreateClient();

        var response = await PutAsync(Request(), StampETag, client: anonymous);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Cough", await fixture.RootScalarAsync<string>("SELECT reason FROM form_encounter"));
    }

    [Fact]
    public async Task A_date_sent_with_a_zone_is_converted_to_server_local_time_like_a_create()
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/encounters/{_uuid}")
        {
            Content = JsonContent.Create(new
            {
                classCode = "AMB", calendarCategoryId = 5, date = "2026-11-12T14:00:00Z", reason = "Cough", facilityId = 3, providerId = 7
            }),
            Headers = { { "If-Match", StampETag } }
        };

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var expected = TimeZoneInfo.ConvertTimeFromUtc(new DateTime(2026, 11, 12, 14, 0, 0, DateTimeKind.Utc), TimeZoneInfo.Local);
        Assert.Equal(expected, await fixture.RootScalarAsync<DateTime>("SELECT date FROM form_encounter"));
    }
}
