using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenEmrNext.ClinicalCare.Api.Contracts.Requests;
using OpenEmrNext.ClinicalCare.Api.Contracts.Responses;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// Found by the slice-1 to 3 layered review (docs/clinical-care/04-layered-review.md): inputs the domain ACCEPTED but a legacy
/// column could not HOLD, which surfaced as a 500 instead of a 400 (DOM-11: limits are invariants and belong in the
/// domain). Each is a value a client can send; none should ever reach the database as an error.
/// </summary>
[Collection("ClinicalCareContainer")]
public class InputRobustnessTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private Guid _encounterUuid;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await fixture.SeedPatientAsync(EncounterTestData.PatientUuid, EncounterTestData.PatientPid);
        _factory = ApiFactory.Create(fixture);
        _client = _factory.ClientWithSecret();
        var created = await (await _client.PostAsJsonAsync("/api/v1/encounters",
            new CreateEncounterRequest(EncounterTestData.PatientUuid, null, 5, null, "visit", 3, null, 7, null, null)))
            .Content.ReadFromJsonAsync<EncounterCreatedResponse>();
        _encounterUuid = created!.Uuid;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private Task<HttpResponseMessage> PostEncounterAsync(string body) =>
        _client.PostAsync("/api/v1/encounters", new StringContent(body, Encoding.UTF8, "application/json"));

    private Task<HttpResponseMessage> PostVitalsAsync(string body) =>
        _client.PostAsync($"/api/v1/encounters/{_encounterUuid}/vitals", new StringContent(body, Encoding.UTF8, "application/json"));

    private static string Encounter(string extra) =>
        $$"""{ "patientUuid": "{{EncounterTestData.PatientUuid}}", "calendarCategoryId": 5, "providerId": 7 {{extra}} }""";

    [Theory]
    [InlineData("0001-01-01T00:00:00")]
    [InlineData("0999-12-31T23:59:59")]
    [InlineData("9999-12-31T23:59:59.9999999")]
    public async Task An_encounter_date_outside_what_the_database_can_store_is_a_400_not_a_500(string date)
    {
        var response = await PostEncounterAsync(Encounter($$""", "date": "{{date}}" """));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_onset_date_outside_what_the_database_can_store_is_a_400_not_a_500()
    {
        var response = await PostEncounterAsync(Encounter(""", "onsetDate": "0001-01-01T00:00:00" """));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_provider_id_larger_than_the_int_column_is_a_400_not_a_500()
    {
        // form_encounter.provider_id is int(11): 3 billion is a valid long and an out-of-range column value.
        var response = await _client.PostAsync("/api/v1/encounters", new StringContent(
            $$"""{ "patientUuid": "{{EncounterTestData.PatientUuid}}", "calendarCategoryId": 5, "providerId": 3000000000 }""",
            Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_vitals_provider_id_larger_than_users_id_can_be_is_a_400()
    {
        var response = await PostVitalsAsync("""{ "pulse": 64, "providerId": 3000000000 }""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_measurement_so_large_that_converting_it_overflows_is_a_400_not_a_500()
    {
        // 7e28 kg * 2.2046 overflows System.Decimal inside the conversion, before any bound is checked.
        var response = await PostVitalsAsync("""{ "weight": { "value": 70000000000000000000000000000, "unit": "kg" }, "providerId": 7 }""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_height_so_small_that_the_derived_bmi_cannot_fit_its_column_is_a_400_not_a_500()
    {
        // weight 1000 lb over 0.001 in gives a BMI near 7e11; form_vitals.BMI is decimal(12,6), max 999999.999999.
        var response = await PostVitalsAsync("""{ "weight": { "value": 1000, "unit": "lb" }, "height": { "value": 0.001, "unit": "in" }, "providerId": 7 }""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_event_with_no_payload_at_all_is_a_400_not_a_500()
    {
        using var producer = _factory.CreateClient();
        producer.DefaultRequestHeaders.Add(ApiFactory.EventsSecretHeader, ApiFactory.EventsSecret);

        var response = await producer.PostAsync("/internal/events", new StringContent(
            $$"""{ "eventId": "{{Guid.NewGuid()}}", "eventType": "PatientRegistered", "schemaVersion": 1 }""", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_event_with_a_nonsense_registration_time_still_records_the_patient_with_the_time_unknown()
    {
        // The timestamp is informational (review finding R-5): bad data in it must not fail, retry and dead-letter a good event.
        using var producer = _factory.CreateClient();
        producer.DefaultRequestHeaders.Add(ApiFactory.EventsSecretHeader, ApiFactory.EventsSecret);
        var patient = Guid.NewGuid();

        var body = $$"""{ "eventId": "{{Guid.NewGuid()}}", "eventType": "PatientRegistered", "schemaVersion": 1, "payload": { "uuid": "{{patient}}", "pid": 99, "registeredAtUtc": "0001-01-01T00:00:00Z" } }""";
        var response = await producer.PostAsync("/internal/events", new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(99L, await fixture.RootScalarAsync<long>($"SELECT pid FROM clinical_care_patient_ref WHERE HEX(patient_uuid) = '{patient.ToString("N").ToUpperInvariant()}'"));
        Assert.Null(await fixture.RootScalarAsync<string>($"SELECT registered_at FROM clinical_care_patient_ref WHERE HEX(patient_uuid) = '{patient.ToString("N").ToUpperInvariant()}'"));
    }
}
