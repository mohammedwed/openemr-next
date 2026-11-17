using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenEmrNext.ClinicalCare.Api.Contracts.Requests;
using OpenEmrNext.ClinicalCare.Api.Contracts.Responses;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// <c>POST /api/v1/allergies</c>, <c>GET /api/v1/allergies/{uuid}</c> and <c>GET /api/v1/allergies</c> through the real HTTP pipeline (auth, binding,
/// validation, use case, adapters, database), as the production principal (QLT-9). Status codes follow API-3.
/// </summary>
[Collection("ClinicalCareContainer")]
public class AllergyEndpointTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await fixture.SeedPatientAsync(EncounterTestData.PatientUuid, EncounterTestData.PatientPid);
        await AllergyTestData.SeedCodeListsAsync(fixture);
        _factory = ApiFactory.Create(fixture);
        _client = _factory.ClientWithSecret();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private static RecordAllergyRequest Valid(
        Guid? patient = null, string? title = "Penicillin", DateTime? begin = null, DateTime? end = null, string? diagnosis = "SNOMED-CT:91936005",
        string? reaction = "hives", string? severity = "severe", string? verification = "confirmed", string? comments = "anaphylaxis", long providerId = 7) =>
        new(patient ?? EncounterTestData.PatientUuid, title, begin, end, diagnosis, reaction, severity, verification, comments, providerId);

    private static async Task<string[]> ErrorsOf(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("errors").EnumerateObject()
            .SelectMany(p => p.Value.EnumerateArray().Select(e => e.GetString()!))
            .ToArray();
    }

    private Task<long> Count(string table, string where = "1=1") => fixture.RootScalarAsync<long>($"SELECT COUNT(*) FROM {table} WHERE {where}");

    private async Task<AllergyRecordedResponse> RecordAsync(RecordAllergyRequest? request = null) =>
        (await (await _client.PostAsJsonAsync("/api/v1/allergies", request ?? Valid())).Content.ReadFromJsonAsync<AllergyRecordedResponse>())!;

    [Fact]
    public async Task POST_allergies_creates_and_returns_201_with_a_location_and_the_identifiers()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/allergies", Valid());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AllergyRecordedResponse>();
        Assert.Equal(EncounterTestData.PatientUuid, body!.PatientUuid);
        Assert.Equal($"/api/v1/allergies/{body.Uuid}", response.Headers.Location?.OriginalString);
        Assert.Equal(1L, await Count("lists", "type = 'allergy'"));
    }

    [Fact]
    public async Task POST_allergies_needs_no_begin_date_unlike_a_problem()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/allergies", Valid(begin: null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task POST_allergies_with_only_a_title_and_a_provider_is_enough()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/allergies", new { patientUuid = EncounterTestData.PatientUuid, title = "Latex", providerId = 7 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("", await fixture.RootScalarAsync<string>("SELECT reaction FROM lists"));
    }

    [Fact]
    public async Task POST_allergies_with_no_gateway_secret_is_401_and_writes_nothing()
    {
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync("/api/v1/allergies", Valid());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0L, await Count("lists"));
    }

    [Fact]
    public async Task POST_allergies_for_an_unknown_patient_is_404_and_leaves_a_failed_audit_row()
    {
        var unknown = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync("/api/v1/allergies", Valid(patient: unknown));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("failed", await fixture.RootScalarAsync<string>("SELECT outcome FROM clinical_care_audit"));
        Assert.Equal("allergy.record", await fixture.RootScalarAsync<string>("SELECT action FROM clinical_care_audit"));
        Assert.Equal(unknown.ToString(), await fixture.RootScalarAsync<string>("SELECT CONCAT(subject, '') FROM clinical_care_audit"));
    }

    [Fact]
    public async Task POST_allergies_for_the_empty_patient_uuid_is_a_404_not_a_500()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/allergies", Valid(patient: Guid.Empty));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(null, "A title is required.")]
    [InlineData("a", "Title must be between 2 and 255 characters.")]
    public async Task POST_allergies_with_a_missing_or_short_title_is_400(string? title, string expected)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/allergies", Valid(title: title));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expected, await ErrorsOf(response));
        Assert.Equal(0L, await Count("lists"));
    }

    [Fact]
    public async Task POST_allergies_reports_every_domain_violation_together()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/allergies",
            Valid(reaction: "rash", severity: "catastrophic", verification: "differential", diagnosis: "not-a-code", begin: new DateTime(2020, 5, 1), end: new DateTime(2019, 1, 1)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ErrorsOf(response);
        Assert.Contains("Reaction 'rash' is not a known reaction.", errors);
        Assert.Contains("Severity 'catastrophic' is not a known severity.", errors);
        Assert.Contains("Verification 'differential' is not a known verification.", errors);
        Assert.Contains("End date cannot be before the begin date.", errors);
        Assert.Contains(errors, e => e.StartsWith("Diagnosis must be", StringComparison.Ordinal));
    }

    [Fact]
    public async Task POST_allergies_with_a_problems_verification_value_is_400_because_the_lists_differ()
    {
        // 'differential' is a condition-verification code in some sites' lists; the allergy list is its own.
        await fixture.RootExecuteAsync("INSERT INTO list_options (list_id, option_id, title, seq, is_default) VALUES ('condition-verification', 'differential', 't', 1, 0)");

        var response = await _client.PostAsJsonAsync("/api/v1/allergies", Valid(verification: "differential"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_allergies_with_an_over_long_code_is_400_at_the_boundary_not_a_database_error()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/allergies", Valid(severity: new string('s', 60)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_allergies_with_a_provider_outside_the_column_range_is_400_not_a_500()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/allergies", Valid(providerId: 3_000_000_000));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SEC_9_a_recorded_allergy_is_audited_with_the_forwarded_address_and_leaves_an_event_and_a_touch_row()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/allergies") { Content = JsonContent.Create(Valid()) };
        request.Headers.Add("X-Forwarded-For", "203.0.113.11");

        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<AllergyRecordedResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(body!.Uuid.ToString(), await fixture.RootScalarAsync<string>("SELECT CONCAT(subject, '') FROM clinical_care_audit"));
        Assert.Equal("203.0.113.11", await fixture.RootScalarAsync<string>("SELECT source_address FROM clinical_care_audit"));
        Assert.Equal(1L, await Count("clinical_care_outbox", "event_type = 'AllergyRecorded'"));
        Assert.Equal(1L, await Count("lists_touch", "type = 'allergy'"));
    }

    [Fact]
    public async Task GET_allergy_returns_what_POST_recorded_with_a_strong_etag_that_is_the_rows_stamp()
    {
        var created = await RecordAsync(Valid(begin: new DateTime(2015, 3, 2), end: new DateTime(2020, 1, 1)));
        await fixture.RootExecuteAsync("UPDATE lists SET modifydate = '2026-01-02 03:04:05'");

        var response = await _client.GetAsync($"/api/v1/allergies/{created.Uuid}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AllergyResponse>();
        Assert.Equal("Penicillin", body!.Title);
        Assert.Equal(new DateTime(2015, 3, 2), body.BeginDate);
        Assert.Equal(new DateTime(2020, 1, 1), body.EndDate);
        Assert.Equal("hives", body.Reaction);
        Assert.Equal("severe", body.Severity);
        Assert.Equal("confirmed", body.Verification);
        Assert.Equal(EncounterTestData.PatientUuid, body.PatientUuid);
        Assert.Equal(ConcurrencyToken.From(new DateTime(2026, 1, 2, 3, 4, 5)).ToETag(), response.Headers.ETag?.ToString());
    }

    [Fact]
    public async Task The_allergys_etag_changes_when_another_writer_edits_the_row()
    {
        var created = await RecordAsync();
        await fixture.RootExecuteAsync("UPDATE lists SET modifydate = '2026-01-02 03:04:05'");
        var before = (await _client.GetAsync($"/api/v1/allergies/{created.Uuid}")).Headers.ETag?.ToString();

        await fixture.RootExecuteAsync("UPDATE lists SET comments = 'edited in the legacy screen'");

        Assert.NotEqual(before, (await _client.GetAsync($"/api/v1/allergies/{created.Uuid}")).Headers.ETag?.ToString());
    }

    [Fact]
    public async Task GET_allergy_for_an_unknown_uuid_is_404_with_no_etag()
    {
        var response = await _client.GetAsync($"/api/v1/allergies/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(response.Headers.ETag);
    }

    [Fact]
    public async Task ADR_0070_GET_allergy_for_the_uuid_of_a_problem_a_medication_or_a_surgery_is_404_and_a_problem_is_not_an_allergy_either_way()
    {
        var problem = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var medication = Guid.Parse("22222222-2222-4222-8222-222222222222");
        var surgery = Guid.Parse("33333333-3333-4333-8333-333333333333");
        await fixture.RootExecuteAsync(
            "INSERT INTO lists (uuid, type, title, pid, activity) VALUES " +
            $"(UNHEX('{problem:N}'), 'medical_problem', 'Asthma', 42, 1), (UNHEX('{medication:N}'), 'medication', 'Aspirin', 42, 1), (UNHEX('{surgery:N}'), 'surgery', 'Appendectomy', 42, 1)");
        var allergy = await RecordAsync();

        foreach (var uuid in new[] { problem, medication, surgery })
            Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/allergies/{uuid}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/problems/{allergy.Uuid}")).StatusCode);
    }

    [Fact]
    public async Task GET_allergies_lists_the_patients_allergies_newest_first_with_paging_fields()
    {
        await RecordAsync(Valid(title: "Older", begin: new DateTime(2010, 1, 1)));
        await RecordAsync(Valid(title: "Newer", begin: new DateTime(2022, 1, 1)));

        var response = await _client.GetAsync($"/api/v1/allergies?patientUuid={EncounterTestData.PatientUuid}&limit=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AllergyListResponse>();
        Assert.Equal(2, body!.Total);
        Assert.Equal(1, body.Limit);
        Assert.Equal("Newer", Assert.Single(body.Items).Title);
        Assert.Null(response.Headers.ETag);
    }

    [Fact]
    public async Task GET_allergies_leaves_out_problems_other_patients_and_rows_with_no_uuid()
    {
        await RecordAsync(Valid(title: "Penicillin"));
        await fixture.RootExecuteAsync(
            "INSERT INTO lists (uuid, type, title, pid, activity) VALUES " +
            "(UNHEX('11111111111141118111111111111111'), 'medical_problem', 'Asthma', 42, 1), " +
            "(UNHEX('33333333333343338333333333333333'), 'allergy', 'Someone elses', 99, 1), " +
            "(NULL, 'allergy', 'No uuid yet', 42, 1)");

        var body = await (await _client.GetAsync($"/api/v1/allergies?patientUuid={EncounterTestData.PatientUuid}"))
            .Content.ReadFromJsonAsync<AllergyListResponse>();

        Assert.Equal(1, body!.Total);
        Assert.Equal("Penicillin", Assert.Single(body.Items).Title);
    }

    [Fact]
    public async Task GET_allergies_without_a_patient_or_with_an_oversized_page_is_400_and_for_an_unknown_patient_404()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync("/api/v1/allergies")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync($"/api/v1/allergies?patientUuid={EncounterTestData.PatientUuid}&limit=10000")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/allergies?patientUuid={Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task The_read_endpoints_need_the_gateway_secret_too()
    {
        using var anonymous = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/v1/allergies/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/v1/allergies?patientUuid={EncounterTestData.PatientUuid}")).StatusCode);
    }
}
