using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenEmrNext.ClinicalCare.Api.Contracts.Requests;
using OpenEmrNext.ClinicalCare.Api.Contracts.Responses;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// <c>/api/v1/encounters/{uuid}/vitals</c> through the real HTTP pipeline, against a real database, as the production
/// principal (QLT-9). The headline behaviour is ADR 0065: units are explicit and nothing is silently misread.
/// </summary>
[Collection("ClinicalCareContainer")]
public class VitalsEndpointTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
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

    private string VitalsUrl => $"/api/v1/encounters/{_encounterUuid}/vitals";

    private static RecordVitalsRequest Request(
        QuantityRequest? weight = null, QuantityRequest? height = null, QuantityRequest? temperature = null,
        int? systolic = null, int? diastolic = null, int? pulse = null, long providerId = 7, string? note = null) =>
        new(weight, height, temperature, null, systolic, diastolic, pulse, null, null, null, null, note, providerId);

    private static async Task<string[]> ErrorsOf(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("errors").EnumerateObject()
            .SelectMany(p => p.Value.EnumerateArray().Select(e => e.GetString()!)).ToArray();
    }

    [Fact]
    public async Task ADR_0065_70_kg_posted_is_stored_as_pounds_and_reads_back_as_either_unit()
    {
        var response = await _client.PostAsJsonAsync(VitalsUrl,
            Request(new(70m, "kg"), new(175m, "cm"), new(37m, "degC"), 120, 80, 64));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<VitalsRecordedResponse>();
        Assert.Equal(22.854881m, created!.Bmi);
        Assert.Equal("Normal", created.BmiStatus);
        Assert.Equal($"{VitalsUrl}/{created.Uuid}", response.Headers.Location?.OriginalString);
        // What legacy would see in the table: US units.
        Assert.Equal("154.323584", await fixture.RootScalarAsync<string>("SELECT weight FROM form_vitals"));

        var us = await (await _client.GetAsync($"{VitalsUrl}/{created.Uuid}")).Content.ReadFromJsonAsync<VitalsResponse>();
        Assert.Equal(new QuantityResponse(154.323584m, "lb"), us!.Weight);
        Assert.Equal(new QuantityResponse(98.6m, "degF"), us.Temperature);
        Assert.Equal(120, us.BloodPressureSystolic);

        var metric = await (await _client.GetAsync($"{VitalsUrl}/{created.Uuid}?units=metric")).Content.ReadFromJsonAsync<VitalsResponse>();
        Assert.Equal("kg", metric!.Weight!.Unit);
        Assert.Equal(70m, metric.Weight.Value, 4);
        Assert.Equal("cm", metric.Height!.Unit);
        Assert.Equal(175m, metric.Height.Value, 4);
        Assert.Equal("degC", metric.Temperature!.Unit);
        Assert.Equal(37m, metric.Temperature.Value, 4);
    }

    [Fact]
    public async Task A_us_unit_request_is_stored_as_given()
    {
        var response = await _client.PostAsJsonAsync(VitalsUrl, Request(new(154m, "lb"), new(70m, "in")));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("154.000000", await fixture.RootScalarAsync<string>("SELECT weight FROM form_vitals"));
        Assert.Equal("70.000000", await fixture.RootScalarAsync<string>("SELECT height FROM form_vitals"));
    }

    [Theory]
    [InlineData("stone")]
    [InlineData("KG")]
    [InlineData("")]
    public async Task An_unknown_or_miscased_unit_is_a_400_never_a_guess(string unit)
    {
        var response = await _client.PostAsJsonAsync(VitalsUrl, Request(new(70m, unit), pulse: 64));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM form_vitals"));
    }

    [Fact]
    public async Task A_unit_for_the_wrong_quantity_is_a_400_naming_the_accepted_ones()
    {
        var response = await _client.PostAsJsonAsync(VitalsUrl, Request(new(70m, "cm")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Weight must be given in lb or kg.", await ErrorsOf(response));
    }

    [Fact]
    public async Task CV_2_a_request_with_no_measurements_is_a_400()
    {
        var response = await _client.PostAsJsonAsync(VitalsUrl, Request(note: "only a note"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("At least one measurement is required.", await ErrorsOf(response));
    }

    [Fact]
    public async Task CV_3_an_implausible_value_is_a_400_listing_every_problem_together()
    {
        var response = await _client.PostAsJsonAsync(VitalsUrl, Request(new(5000m, "lb"), new(500m, "in"), pulse: 64));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ErrorsOf(response);
        Assert.Equal(2, errors.Length);
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM form_vitals"));
    }

    [Fact]
    public async Task ARC_11_a_binding_level_violation_uses_the_same_message_the_domain_would()
    {
        var response = await _client.PostAsJsonAsync(VitalsUrl, Request(systolic: 0, pulse: 64));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Systolic blood pressure must be between 1 and 400.", await ErrorsOf(response));
    }

    [Fact]
    public async Task CV_1_vitals_for_an_unknown_encounter_are_a_404_problem_json()
    {
        var response = await _client.PostAsJsonAsync($"/api/v1/encounters/{Guid.NewGuid()}/vitals", Request(pulse: 64));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task CV_1_vitals_for_a_deleted_encounter_are_a_404()
    {
        await fixture.RootExecuteAsync("UPDATE forms SET deleted = 1 WHERE formdir = 'newpatient'");

        var response = await _client.PostAsJsonAsync(VitalsUrl, Request(pulse: 64));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_list_returns_newest_first_with_paging_and_defaults_to_us_units()
    {
        await _client.PostAsJsonAsync(VitalsUrl, Request(new(150m, "lb")));
        await _client.PostAsJsonAsync(VitalsUrl, Request(new(160m, "lb")));

        var response = await _client.GetAsync($"{VitalsUrl}?limit=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<VitalsListResponse>();
        Assert.Single(body!.Items);
        Assert.Equal(2, body.Total);
        Assert.Equal(1, body.Limit);
        Assert.Equal("lb", body.Items[0].Weight!.Unit);
    }

    [Fact]
    public async Task A_list_with_an_oversized_limit_or_unknown_units_is_a_400()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync($"{VitalsUrl}?limit=10000")).StatusCode);
        var units = await _client.GetAsync($"{VitalsUrl}?units=furlongs");
        Assert.Equal(HttpStatusCode.BadRequest, units.StatusCode);
        Assert.Contains("units must be 'us' or 'metric'.", await ErrorsOf(units));
    }

    [Fact]
    public async Task Getting_vitals_that_do_not_exist_or_belong_to_another_encounter_is_a_404()
    {
        // A second encounter for the same patient: its vitals must not be readable through the first one's URL.
        var other = await (await _client.PostAsJsonAsync("/api/v1/encounters",
            new CreateEncounterRequest(EncounterTestData.PatientUuid, null, 5, null, "other", 3, null, 7, null, null)))
            .Content.ReadFromJsonAsync<EncounterCreatedResponse>();
        var otherVitals = await (await _client.PostAsJsonAsync($"/api/v1/encounters/{other!.Uuid}/vitals", Request(pulse: 70)))
            .Content.ReadFromJsonAsync<VitalsRecordedResponse>();

        var wrongEncounter = await _client.GetAsync($"{VitalsUrl}/{otherVitals!.Uuid}");
        var nonexistent = await _client.GetAsync($"{VitalsUrl}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, wrongEncounter.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, nonexistent.StatusCode);
    }

    [Fact]
    public async Task Every_vitals_endpoint_requires_the_gateway_secret()
    {
        using var anonymous = _factory.CreateClient();
        var body = JsonContent.Create(Request(pulse: 64));

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync(VitalsUrl, body)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(VitalsUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"{VitalsUrl}/{Guid.NewGuid()}")).StatusCode);
    }
}
