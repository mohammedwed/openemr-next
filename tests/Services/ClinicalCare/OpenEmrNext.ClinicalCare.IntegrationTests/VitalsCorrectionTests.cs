using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenEmrNext.ClinicalCare.Api.Contracts.Requests;
using OpenEmrNext.ClinicalCare.Api.Contracts.Responses;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;
using OpenEmrNext.ClinicalCare.Domain.VitalSigns;
using OpenEmrNext.ClinicalCare.Infrastructure;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// The vitals update adapter against a real MariaDB, as the production principal (ADR 0068): a conditional write of only the measurement columns, the event and the audit row in one transaction,
/// and the ways it must refuse. Above all, a correction can reach only vitals that belong to its own encounter and patient and are not soft-deleted.
/// </summary>
[Collection("ClinicalCareContainer")]
public class VitalsUpdaterTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 11, 17, 9, 30, 0);
    private static readonly DateTime Stamp = new(2026, 1, 2, 3, 4, 5);
    private static readonly ConcurrencyToken Token = ConcurrencyToken.From(Stamp);

    private Vitals _vitals = null!;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        _vitals = VitalsRegistryTests.NewVitals();   // encounter 1001, patient pid 42, 70 kg, 175 cm, pulse 64, bp 120/80, note "calm"
        await using var db = fixture.CreateDbContext();
        await new EfVitalsRegistry(db, new EfAuditLog(db)).RegisterAsync(
            _vitals, new(_vitals.Uuid, _vitals.Encounter.Uuid, 1001, _vitals.Encounter.Patient.Uuid, 42, _vitals.MeasurementsTaken, Now),
            VitalsRegistryTests.AuditFor(_vitals), CancellationToken.None);

        // A known stamp to base the correction on, and a clean slate for the audit and outbox so each test counts only its own rows.
        await fixture.RootExecuteAsync($"UPDATE form_vitals SET last_updated = '{Stamp:yyyy-MM-dd HH:mm:ss}'");
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_audit");
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_outbox");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static VitalsChange Change(VitalsInput? input = null) =>
        VitalsChange.Create(input ?? new VitalsInput(
            Weight: new Quantity(80m, MeasurementUnit.Kilogram), Height: new Quantity(180m, MeasurementUnit.Centimeter),
            Temperature: new Quantity(37.5m, MeasurementUnit.DegreeCelsius), TemperatureMethod: "Axillary", Systolic: 130, Diastolic: 85, Pulse: 70,
            Respiration: 18, OxygenSaturation: 97m, WaistCircumference: new Quantity(85m, MeasurementUnit.Centimeter),
            HeadCircumference: new Quantity(56m, MeasurementUnit.Centimeter), Note: "rechecked"));

    private VitalsUpdateRequest Request(
        VitalsChange? change = null, ConcurrencyToken? expected = null, Guid? uuid = null, long? number = null, long? pid = null)
    {
        var id = uuid ?? _vitals.Uuid;
        var names = new[] { "weight", "pulse" };
        return new VitalsUpdateRequest(
            id, number ?? 1001, pid ?? 42, expected ?? Token, change ?? Change(),
            new VitalsCorrectedEvent(id, _vitals.Encounter.Uuid, number ?? 1001, _vitals.Encounter.Patient.Uuid, pid ?? 42, names, Now),
            new AuditEntry(Now, AuditActor.Unknown, AuditActions.VitalsCorrect, id, AuditOutcome.Success, "corr-vc", "10.9.9.9", names));
    }

    private async Task<UpdateOutcome> UpdateAsync(VitalsUpdateRequest request)
    {
        await using var db = fixture.CreateDbContext();
        return await new EfVitalsUpdater(db, new EfAuditLog(db)).UpdateAsync(request, CancellationToken.None);
    }

    private Task<T?> Col<T>(string column) => fixture.RootScalarAsync<T>($"SELECT {column} FROM form_vitals");

    [Fact]
    public async Task A_correction_changes_every_measurement_column_recomputes_the_bmi_and_returns_the_new_stamp()
    {
        var outcome = await UpdateAsync(Request());

        Assert.Equal(UpdateStatus.Updated, outcome.Status);
        Assert.Equal(176.369810m, await Col<decimal>("weight"));
        Assert.Equal(70.866142m, await Col<decimal>("height"));
        Assert.Equal(99.5m, await Col<decimal>("temperature"));
        Assert.Equal("Axillary", await Col<string>("temp_method"));
        Assert.Equal("130", await Col<string>("bps"));
        Assert.Equal("85", await Col<string>("bpd"));
        Assert.Equal(70m, await Col<decimal>("pulse"));
        Assert.Equal(18m, await Col<decimal>("respiration"));
        Assert.Equal(97m, await Col<decimal>("oxygen_saturation"));
        Assert.Equal(33.464567m, await Col<decimal>("waist_circ"));
        Assert.Equal(22.047244m, await Col<decimal>("head_circ"));
        Assert.Equal("rechecked", await Col<string>("note"));
        Assert.Equal(await Col<decimal>("BMI"), Change().Bmi);
        Assert.Equal(Change().BmiStatus, await Col<string>("BMI_status"));
        var stored = await Col<DateTime>("last_updated");
        Assert.True(stored > Stamp);
        Assert.Equal(stored, outcome.Stamp);
    }

    [Fact]
    public async Task A_measurement_left_out_is_stored_as_zero_the_legacy_not_recorded_value_and_the_bmi_goes_with_it()
    {
        await UpdateAsync(Request(change: Change(new VitalsInput(Weight: new Quantity(80m, MeasurementUnit.Kilogram), Pulse: 70))));

        Assert.Equal(0m, await Col<decimal>("height"));
        Assert.Equal(0m, await Col<decimal>("temperature"));
        Assert.Equal(0m, await Col<decimal>("respiration"));
        Assert.Equal(0m, await Col<decimal>("BMI"));
        Assert.Null(await Col<string?>("BMI_status"));
        Assert.Null(await Col<string?>("bps"));
        Assert.Null(await Col<string?>("note"));
    }

    [Fact]
    public async Task A_correction_writes_its_event_and_its_audit_row_in_the_same_commit_naming_measurements_only()
    {
        await UpdateAsync(Request());

        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_outbox WHERE event_type = 'VitalsCorrected'"));
        var payload = await fixture.RootScalarAsync<string>("SELECT payload FROM clinical_care_outbox");
        Assert.Contains("weight", payload);
        Assert.DoesNotContain("176", payload);
        Assert.DoesNotContain("rechecked", payload);
        Assert.Equal("vitals.correct", await fixture.RootScalarAsync<string>("SELECT action FROM clinical_care_audit"));
        Assert.Equal(_vitals.Uuid.ToString(), await fixture.RootScalarAsync<string>("SELECT CONCAT(subject, '') FROM clinical_care_audit"));
        Assert.Equal("""["weight","pulse"]""", await fixture.RootScalarAsync<string>("SELECT details FROM clinical_care_audit"));
        Assert.Equal("unknown", await fixture.RootScalarAsync<string>("SELECT actor FROM clinical_care_audit"));
    }

    [Fact]
    public async Task Who_recorded_the_vitals_and_when_and_the_forms_row_are_left_exactly_as_they_were()
    {
        var identity = "SELECT CONCAT(HEX(uuid), '|', pid, '|', `date`, '|', IFNULL(`user`, ''), '|', IFNULL(groupname, ''), '|', authorized, '|', activity) FROM form_vitals";
        var forms = "SELECT CONCAT(`date`, '|', encounter, '|', form_name, '|', form_id, '|', pid, '|', IFNULL(`user`, ''), '|', authorized, '|', deleted, '|', formdir) FROM forms";
        var identityBefore = await fixture.RootScalarAsync<string>(identity);
        var formsBefore = await fixture.RootScalarAsync<string>(forms);

        await UpdateAsync(Request());

        Assert.Equal(identityBefore, await fixture.RootScalarAsync<string>(identity));
        Assert.Equal(formsBefore, await fixture.RootScalarAsync<string>(forms));
    }

    [Fact]
    public async Task A_stale_stamp_is_a_conflict_that_reports_the_current_stamp_and_writes_nothing()
    {
        var outcome = await UpdateAsync(Request(expected: ConcurrencyToken.From(Stamp.AddDays(-3))));

        Assert.Equal(UpdateStatus.Conflict, outcome.Status);
        Assert.Equal(Stamp, outcome.Stamp);
        Assert.Equal(64m, await Col<decimal>("pulse"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_outbox"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_audit"));
    }

    [Fact]
    public async Task LEG_10_a_legacy_edit_between_the_read_and_the_write_is_detected_and_not_overwritten()
    {
        await fixture.RootExecuteAsync("UPDATE form_vitals SET note = 'edited in the legacy screen'");   // the database moves last_updated

        var outcome = await UpdateAsync(Request());

        Assert.Equal(UpdateStatus.Conflict, outcome.Status);
        Assert.NotEqual(Stamp, outcome.Stamp);
        Assert.Equal("edited in the legacy screen", await Col<string>("note"));
        Assert.Equal(64m, await Col<decimal>("pulse"));
    }

    [Fact]
    public async Task A_vitals_uuid_that_does_not_exist_is_not_found()
    {
        var outcome = await UpdateAsync(Request(uuid: Guid.NewGuid()));

        Assert.Equal(UpdateStatus.NotFound, outcome.Status);
        Assert.Null(outcome.Stamp);
    }

    [Fact]
    public async Task A_correction_cannot_reach_vitals_of_another_encounter_or_another_patient_by_their_uuid()
    {
        var otherEncounter = await UpdateAsync(Request(number: 2002));
        var otherPatient = await UpdateAsync(Request(pid: 99));

        Assert.Equal(UpdateStatus.NotFound, otherEncounter.Status);
        Assert.Equal(UpdateStatus.NotFound, otherPatient.Status);
        Assert.Equal(64m, await Col<decimal>("pulse"));
        Assert.Equal("calm", await Col<string>("note"));
    }

    [Fact]
    public async Task Vitals_legacy_has_soft_deleted_are_not_found_and_stay_as_they_were()
    {
        await fixture.RootExecuteAsync("UPDATE forms SET deleted = 1 WHERE formdir = 'vitals'");
        await fixture.RootExecuteAsync($"UPDATE form_vitals SET last_updated = '{Stamp:yyyy-MM-dd HH:mm:ss}'");

        var outcome = await UpdateAsync(Request());

        Assert.Equal(UpdateStatus.NotFound, outcome.Status);
        Assert.Equal(64m, await Col<decimal>("pulse"));
    }

    [Fact]
    public async Task Writing_identical_values_still_matches_because_the_driver_counts_matched_rows_not_changed_ones()
    {
        var same = Change(new VitalsInput(
            Weight: new Quantity(70m, MeasurementUnit.Kilogram), Height: new Quantity(175m, MeasurementUnit.Centimeter),
            Temperature: new Quantity(37m, MeasurementUnit.DegreeCelsius), TemperatureMethod: "Oral", Systolic: 120, Diastolic: 80, Pulse: 64,
            Respiration: 16, OxygenSaturation: 98m, Note: "calm"));

        var outcome = await UpdateAsync(Request(change: same));

        Assert.Equal(UpdateStatus.Updated, outcome.Status);
    }

    [Fact]
    public async Task SEC_9_a_correction_cannot_commit_without_its_audit_row()
    {
        await using var db = fixture.CreateDbContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new EfVitalsUpdater(db, new ThrowingAuditLog()).UpdateAsync(Request(), CancellationToken.None));

        Assert.Equal(64m, await Col<decimal>("pulse"));
        Assert.Equal(Stamp, await Col<DateTime>("last_updated"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_outbox"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_audit"));
    }
}

/// <summary>
/// <c>PUT /api/v1/encounters/{encounterUuid}/vitals/{vitalsUuid}</c> through the real HTTP pipeline, as the production principal (QLT-9): 200 with the new <c>ETag</c>, 428, 412, 400, 404 and 401, the
/// same contract as the other updates (ADR 0068), in either unit system.
/// </summary>
[Collection("ClinicalCareContainer")]
public class VitalsCorrectionEndpointTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    private static readonly DateTime Stamp = new(2026, 1, 2, 3, 4, 5);
    private static readonly string StampETag = ConcurrencyToken.From(Stamp).ToETag();

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private Guid _encounter;
    private Guid _vitals;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await fixture.SeedPatientAsync(EncounterTestData.PatientUuid, EncounterTestData.PatientPid);
        _factory = ApiFactory.Create(fixture);
        _client = _factory.ClientWithSecret();

        _encounter = (await (await _client.PostAsJsonAsync(
                "/api/v1/encounters", new CreateEncounterRequest(EncounterTestData.PatientUuid, null, 5, null, "visit", 3, null, 7, null, null)))
            .Content.ReadFromJsonAsync<EncounterCreatedResponse>())!.Uuid;
        _vitals = (await (await _client.PostAsJsonAsync(
                $"/api/v1/encounters/{_encounter}/vitals",
                new RecordVitalsRequest(
                    new QuantityRequest(70m, "kg"), new QuantityRequest(175m, "cm"), null, null, 120, 80, 64, null, null, null, null, "calm", 7)))
            .Content.ReadFromJsonAsync<VitalsRecordedResponse>())!.Uuid;

        await fixture.RootExecuteAsync($"UPDATE form_vitals SET last_updated = '{Stamp:yyyy-MM-dd HH:mm:ss}'");
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_audit");
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_outbox");
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private static CorrectVitalsRequest Body(int? pulse = 72, string? note = "rechecked", QuantityRequest? weight = null, QuantityRequest? height = null, int? systolic = 120) =>
        new(weight ?? new QuantityRequest(70m, "kg"), height ?? new QuantityRequest(175m, "cm"), null, null, systolic, 80, pulse, null, null, null, null, note);

    private Task<HttpResponseMessage> PutAsync(CorrectVitalsRequest body, string? ifMatch = null, Guid? vitals = null, Guid? encounter = null, string query = "", HttpClient? client = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/encounters/{encounter ?? _encounter}/vitals/{vitals ?? _vitals}{query}") { Content = JsonContent.Create(body) };
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
        var response = await PutAsync(Body(pulse: 72, note: "rechecked"), StampETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<VitalsResponse>();
        Assert.Equal(_vitals, body!.Uuid);
        Assert.Equal(72, body.Pulse);
        Assert.Equal("rechecked", body.Note);
        Assert.Equal(154.323584m, body.Weight!.Value);   // the default is US units, the stored ones (ADR 0065): 70 kg
        Assert.Equal("lb", body.Weight.Unit);
        var newETag = response.Headers.ETag?.ToString();
        Assert.NotNull(newETag);
        Assert.NotEqual(StampETag, newETag);
        Assert.Equal(newETag, body.ETag);
        Assert.Equal(newETag, (await _client.GetAsync($"/api/v1/encounters/{_encounter}/vitals/{_vitals}")).Headers.ETag?.ToString());
    }

    [Fact]
    public async Task A_correction_in_pounds_is_stored_in_pounds_and_reads_back_as_either_unit()
    {
        var response = await PutAsync(Body(weight: new QuantityRequest(154.323584m, "lb"), height: new QuantityRequest(68.897638m, "in"), pulse: 64, note: "calm"), StampETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);   // the same stored values as 70 kg and 175 cm: nothing changed
        Assert.Equal(StampETag, response.Headers.ETag?.ToString());
    }

    [Fact]
    public async Task The_response_can_be_asked_for_in_metric()
    {
        var response = await PutAsync(Body(pulse: 80), StampETag, query: "?units=metric");

        var body = await response.Content.ReadFromJsonAsync<VitalsResponse>();
        Assert.Equal("kg", body!.Weight!.Unit);
        Assert.Equal(70m, body.Weight.Value);
        Assert.Equal("cm", body.Height!.Unit);
    }

    [Fact]
    public async Task A_reading_can_be_removed_by_leaving_it_out_and_the_bmi_goes_with_the_height()
    {
        var response = await PutAsync(new CorrectVitalsRequest(new QuantityRequest(70m, "kg"), null, null, null, 120, 80, 64, null, null, null, null, "calm"), StampETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<VitalsResponse>();
        Assert.Null(body!.Height);
        Assert.Null(body.Bmi);
        Assert.Equal(0m, await fixture.RootScalarAsync<decimal>("SELECT height FROM form_vitals"));
    }

    [Fact]
    public async Task PUT_with_no_if_match_a_wildcard_or_a_blank_one_is_428_and_writes_nothing()
    {
        foreach (var header in new string?[] { null, "*", "", "   " })
            Assert.Equal((HttpStatusCode)428, (await PutAsync(Body(), header)).StatusCode);

        Assert.Equal(64m, await fixture.RootScalarAsync<decimal>("SELECT pulse FROM form_vitals"));
        Assert.Equal(0L, await Count("clinical_care_audit"));
    }

    [Fact]
    public async Task PUT_with_a_stale_or_unreadable_etag_is_412_the_stale_one_carries_the_current_etag_and_nothing_is_written()
    {
        var stale = await PutAsync(Body(), ConcurrencyToken.From(Stamp.AddDays(-5)).ToETag());

        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal(StampETag, stale.Headers.ETag?.ToString());
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await PutAsync(Body(), "garbage")).StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await PutAsync(Body(), "W/" + StampETag)).StatusCode);
        Assert.Equal(64m, await fixture.RootScalarAsync<decimal>("SELECT pulse FROM form_vitals"));
        Assert.Equal(0L, await Count("clinical_care_outbox"));
    }

    [Fact]
    public async Task LEG_10_a_legacy_edit_after_the_read_makes_the_correction_a_412_and_the_other_writers_value_survives()
    {
        var readTag = (await _client.GetAsync($"/api/v1/encounters/{_encounter}/vitals/{_vitals}")).Headers.ETag!.ToString();
        await fixture.RootExecuteAsync("UPDATE form_vitals SET note = 'edited in the legacy screen'");

        var response = await PutAsync(Body(), readTag);

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        Assert.Equal("edited in the legacy screen", await fixture.RootScalarAsync<string>("SELECT note FROM form_vitals"));
        Assert.NotEqual(readTag, response.Headers.ETag?.ToString());
    }

    [Fact]
    public async Task PUT_for_unknown_vitals_an_unknown_encounter_or_vitals_of_another_encounter_is_404()
    {
        var other = (await (await _client.PostAsJsonAsync(
                "/api/v1/encounters", new CreateEncounterRequest(EncounterTestData.PatientUuid, null, 5, null, "another visit", 3, null, 7, null, null)))
            .Content.ReadFromJsonAsync<EncounterCreatedResponse>())!.Uuid;

        Assert.Equal(HttpStatusCode.NotFound, (await PutAsync(Body(), StampETag, vitals: Guid.NewGuid())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PutAsync(Body(), StampETag, encounter: Guid.NewGuid())).StatusCode);
        // The vitals exist, but belong to the first encounter: through the second they are not found, and nothing changes.
        Assert.Equal(HttpStatusCode.NotFound, (await PutAsync(Body(), StampETag, encounter: other)).StatusCode);
        Assert.Equal(64m, await fixture.RootScalarAsync<decimal>("SELECT pulse FROM form_vitals"));
    }

    [Fact]
    public async Task PUT_with_invalid_measurements_is_400_with_every_message_and_writes_nothing()
    {
        var response = await PutAsync(Body(weight: new QuantityRequest(0m, "kg"), height: new QuantityRequest(1m, "in")), StampETag);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ErrorsOf(response);
        Assert.Contains(errors, e => e.StartsWith("Weight must be", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("Height must be", StringComparison.Ordinal));
        Assert.Equal(64m, await fixture.RootScalarAsync<decimal>("SELECT pulse FROM form_vitals"));
    }

    [Fact]
    public async Task PUT_with_an_unknown_unit_or_no_measurements_at_all_is_400()
    {
        var badUnit = await PutAsync(Body(weight: new QuantityRequest(70m, "stone")), StampETag);
        var empty = await PutAsync(new CorrectVitalsRequest(null, null, null, null, null, null, null, null, null, null, null, null), StampETag);

        Assert.Equal(HttpStatusCode.BadRequest, badUnit.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Contains("At least one measurement is required.", await ErrorsOf(empty));
        Assert.Equal(64m, await fixture.RootScalarAsync<decimal>("SELECT pulse FROM form_vitals"));
    }

    [Fact]
    public async Task The_order_of_checks_is_a_malformed_body_then_428_then_412_then_domain_rules()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await PutAsync(Body(pulse: 9999))).StatusCode);                                       // binding: pulse out of range, and no If-Match
        Assert.Equal((HttpStatusCode)428, (await PutAsync(Body(weight: new QuantityRequest(0m, "kg")))).StatusCode);                    // domain-invalid but no precondition
        Assert.Equal(
            HttpStatusCode.PreconditionFailed,
            (await PutAsync(Body(weight: new QuantityRequest(0m, "kg")), ConcurrencyToken.From(Stamp.AddDays(-5)).ToETag())).StatusCode);
    }

    [Fact]
    public async Task PUT_of_what_is_already_stored_is_200_with_the_same_etag_and_writes_no_event_or_audit_row()
    {
        var response = await PutAsync(Body(pulse: 64, note: "calm"), StampETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(StampETag, response.Headers.ETag?.ToString());
        Assert.Equal(0L, await Count("clinical_care_outbox"));
        Assert.Equal(0L, await Count("clinical_care_audit"));
    }

    [Fact]
    public async Task SEC_9_a_correction_leaves_one_success_row_naming_measurements_and_no_values_and_one_event_and_a_412_and_a_404_each_one_failed_row()
    {
        await PutAsync(Body(pulse: 188, note: "secret detail"), StampETag);
        await PutAsync(Body(), ConcurrencyToken.From(Stamp.AddDays(-5)).ToETag());
        await PutAsync(Body(), StampETag, vitals: Guid.NewGuid());

        Assert.Equal(1L, await Count("clinical_care_audit", "outcome = 'success' AND action = 'vitals.correct'"));
        Assert.Equal(2L, await Count("clinical_care_audit", "outcome = 'failed' AND action = 'vitals.correct'"));
        var details = await fixture.RootScalarAsync<string>("SELECT details FROM clinical_care_audit WHERE outcome = 'success'");
        Assert.Contains("pulse", details);
        Assert.DoesNotContain("188", details);
        Assert.DoesNotContain("secret", details);
        Assert.Equal(1L, await Count("clinical_care_outbox", "event_type = 'VitalsCorrected'"));
    }

    [Fact]
    public async Task PUT_leaves_who_recorded_the_vitals_untouched()
    {
        var before = await fixture.RootScalarAsync<string>("SELECT CONCAT(IFNULL(`user`, ''), '|', IFNULL(groupname, ''), '|', `date`, '|', authorized) FROM form_vitals");

        await PutAsync(Body(pulse: 90), StampETag);

        Assert.Equal(before, await fixture.RootScalarAsync<string>("SELECT CONCAT(IFNULL(`user`, ''), '|', IFNULL(groupname, ''), '|', `date`, '|', authorized) FROM form_vitals"));
    }

    [Fact]
    public async Task PUT_with_no_gateway_secret_is_401_and_writes_nothing()
    {
        using var anonymous = _factory.CreateClient();

        var response = await PutAsync(Body(), StampETag, client: anonymous);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(64m, await fixture.RootScalarAsync<decimal>("SELECT pulse FROM form_vitals"));
    }

    [Fact]
    public async Task The_list_items_carry_their_etag_and_it_is_the_one_a_correction_needs()
    {
        var list = await (await _client.GetAsync($"/api/v1/encounters/{_encounter}/vitals")).Content.ReadFromJsonAsync<VitalsListResponse>();
        var item = Assert.Single(list!.Items);

        var response = await PutAsync(Body(pulse: 66), item.ETag);

        Assert.Equal(StampETag, item.ETag);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
