using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenEmrNext.ClinicalCare.Api.Contracts.Requests;
using OpenEmrNext.ClinicalCare.Api.Contracts.Responses;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// <c>POST /api/v1/encounters</c> through the real HTTP pipeline (auth, binding, validation, use case, adapters,
/// database), as the production principal (QLT-9). Status codes follow API-3.
/// </summary>
[Collection("ClinicalCareContainer")]
public class EncounterEndpointTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    // Always set by InitializeAsync before any test body runs.
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
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

    private static CreateEncounterRequest Valid(
        Guid? patient = null, string? classCode = null, int category = 5, int facilityId = 3, long providerId = 7,
        int? pos = null, string? sensitivity = null, string? reason = "Cough") =>
        new(patient ?? EncounterTestData.PatientUuid, classCode, category, null, reason, facilityId, pos,
            providerId, null, sensitivity);

    private static async Task<string[]> ErrorsOf(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("errors").EnumerateObject()
            .SelectMany(p => p.Value.EnumerateArray().Select(e => e.GetString()!))
            .ToArray();
    }

    [Fact]
    public async Task POST_encounters_creates_and_returns_201_with_a_location_and_the_identifiers()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/encounters", Valid());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<EncounterCreatedResponse>();
        Assert.NotNull(body);
        Assert.Equal(1001, body.Number);
        Assert.Equal(EncounterTestData.PatientUuid, body.PatientUuid);
        Assert.Equal($"/api/v1/encounters/{body.Uuid}", response.Headers.Location?.OriginalString);
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM form_encounter"));
    }

    [Fact]
    public async Task POST_encounters_with_no_gateway_secret_returns_401_and_writes_nothing()
    {
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync("/api/v1/encounters", Valid());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM form_encounter"));
    }

    [Fact]
    public async Task POST_encounters_with_the_wrong_gateway_secret_returns_401()
    {
        using var wrong = _factory.CreateClient();
        wrong.DefaultRequestHeaders.Add(ApiFactory.SecretHeader, "not-the-secret");

        var response = await wrong.PostAsJsonAsync("/api/v1/encounters", Valid());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_encounters_for_an_unknown_patient_returns_404_problem_json()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/encounters", Valid(patient: Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task POST_encounters_with_a_domain_rule_violation_returns_400_listing_the_message()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/encounters", Valid(classCode: "XYZ"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Class 'XYZ' is not a known encounter class.", await ErrorsOf(response));
    }

    [Fact]
    public async Task POST_encounters_with_an_unknown_facility_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/encounters", Valid(facilityId: 99));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Facility 99 does not exist.", await ErrorsOf(response));
    }

    [Fact]
    public async Task ARC_11_a_binding_level_violation_uses_the_same_message_the_domain_would()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/encounters", Valid(category: 0));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("A calendar category is required.", await ErrorsOf(response));
    }

    [Fact]
    public async Task POST_encounters_reports_every_binding_error_not_just_the_first()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/encounters", Valid(category: 0, providerId: 0, pos: 500));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ErrorsOf(response);
        Assert.Contains("A calendar category is required.", errors);
        Assert.Contains("ProviderId must identify the acting provider.", errors);
        Assert.Contains("PosCode must be between 1 and 99.", errors);
    }

    [Fact]
    public async Task A_rejected_request_takes_no_number_from_the_shared_counter()
    {
        await _client.PostAsJsonAsync("/api/v1/encounters", Valid(classCode: "XYZ"));

        Assert.Equal(1000L, await fixture.RootScalarAsync<long>("SELECT id FROM sequences"));
    }

    [Fact]
    public async Task OPS_3_the_service_refuses_to_start_without_its_gateway_secret()
    {
        using var factory = ApiFactory.Create(fixture, secret: null);

        // Options validation runs on start; creating the client starts the host.
        await Assert.ThrowsAnyAsync<Exception>(() => Task.Run(() => factory.CreateClient()));
    }

    [Fact]
    public async Task GET_encounter_returns_what_POST_created_so_it_reads_back_end_to_end()
    {
        var created = await (await _client.PostAsJsonAsync("/api/v1/encounters", Valid(reason: "Cough")))
            .Content.ReadFromJsonAsync<EncounterCreatedResponse>();

        var response = await _client.GetAsync($"/api/v1/encounters/{created!.Uuid}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<EncounterResponse>();
        Assert.Equal(created.Uuid, body!.Uuid);
        Assert.Equal(created.Number, body.Number);
        Assert.Equal(EncounterTestData.PatientUuid, body.PatientUuid);
        Assert.Equal("AMB", body.ClassCode);
        Assert.Equal("Main Clinic", body.FacilityName);
        Assert.Equal("Cough", body.Reason);
    }

    [Fact]
    public async Task GET_encounter_for_an_unknown_uuid_returns_404_problem_json()
    {
        var response = await _client.GetAsync($"/api/v1/encounters/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GET_encounter_for_one_legacy_has_deleted_returns_404()
    {
        var created = await (await _client.PostAsJsonAsync("/api/v1/encounters", Valid()))
            .Content.ReadFromJsonAsync<EncounterCreatedResponse>();
        // What legacy's delete does: flag the newpatient forms row, leave form_encounter alone (CC-9).
        await fixture.RootExecuteAsync("UPDATE forms SET deleted = 1 WHERE formdir = 'newpatient'");

        var response = await _client.GetAsync($"/api/v1/encounters/{created!.Uuid}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GET_encounters_lists_the_patients_encounters_with_paging_metadata()
    {
        await _client.PostAsJsonAsync("/api/v1/encounters", Valid());
        await _client.PostAsJsonAsync("/api/v1/encounters", Valid());

        var response = await _client.GetAsync($"/api/v1/encounters?patientUuid={EncounterTestData.PatientUuid}&limit=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<EncounterListResponse>();
        Assert.Single(body!.Items);
        Assert.Equal(2, body.Total);
        Assert.Equal(1, body.Limit);
        Assert.Equal(0, body.Offset);
    }

    [Fact]
    public async Task GET_encounters_without_a_patient_returns_400_there_is_no_unbounded_all_encounters_read()
    {
        var response = await _client.GetAsync("/api/v1/encounters");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("patientUuid is required.", await ErrorsOf(response));
    }

    [Fact]
    public async Task GET_encounters_with_an_oversized_limit_returns_400()
    {
        var response = await _client.GetAsync($"/api/v1/encounters?patientUuid={EncounterTestData.PatientUuid}&limit=10000");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Limit must be between 1 and 100.", await ErrorsOf(response));
    }

    [Fact]
    public async Task GET_encounters_for_an_unknown_patient_returns_404()
    {
        var response = await _client.GetAsync($"/api/v1/encounters?patientUuid={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GET_endpoints_also_require_the_gateway_secret()
    {
        using var anonymous = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/v1/encounters/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/v1/encounters?patientUuid={EncounterTestData.PatientUuid}")).StatusCode);
    }

    [Fact]
    public async Task OPS_3_the_service_refuses_to_start_without_its_events_secret_so_it_never_silently_accepts_events_from_anyone()
    {
        using var factory = ApiFactory.Create(fixture, eventsSecret: null);

        await Assert.ThrowsAnyAsync<Exception>(() => Task.Run(() => factory.CreateClient()));
    }
}
