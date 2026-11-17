using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenEmrNext.ClinicalCare.Api.Contracts.Requests;
using OpenEmrNext.ClinicalCare.Api.Contracts.Responses;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// <c>POST /api/v1/problems</c>, <c>GET /api/v1/problems/{uuid}</c> and <c>GET /api/v1/problems</c> through the real HTTP pipeline (auth, binding,
/// validation, use case, adapters, database), as the production principal (QLT-9). Status codes follow API-3.
/// </summary>
[Collection("ClinicalCareContainer")]
public class ProblemEndpointTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await fixture.SeedPatientAsync(EncounterTestData.PatientUuid, EncounterTestData.PatientPid);
        // The outcome and occurrence lists a site has configured (the snapshot's values).
        foreach (var o in new[] { "0", "1", "2", "3", "4", "5" })
            await fixture.RootExecuteAsync($"INSERT INTO list_options (list_id, option_id, title, seq, is_default) VALUES ('outcome', '{o}', 't', {o}, 0)");
        foreach (var o in new[] { "0", "1", "4", "5", "6", "7", "8" })
            await fixture.RootExecuteAsync($"INSERT INTO list_options (list_id, option_id, title, seq, is_default) VALUES ('occurrence', '{o}', 't', {o}, 0)");
        _factory = ApiFactory.Create(fixture);
        _client = _factory.ClientWithSecret();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private static RecordProblemRequest Valid(
        Guid? patient = null, string? title = "Asthma", DateTime? begin = null, DateTime? end = null, string? diagnosis = "ICD10:J45.909",
        int outcome = 0, int occurrence = 1, string? referredBy = null, string? comments = "seasonal", long providerId = 7) =>
        new(patient ?? EncounterTestData.PatientUuid, title, begin ?? new DateTime(2020, 5, 1), end, diagnosis, outcome, occurrence, referredBy, comments, providerId);

    private static async Task<string[]> ErrorsOf(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("errors").EnumerateObject()
            .SelectMany(p => p.Value.EnumerateArray().Select(e => e.GetString()!))
            .ToArray();
    }

    private Task<long> Count(string table, string where = "1=1") => fixture.RootScalarAsync<long>($"SELECT COUNT(*) FROM {table} WHERE {where}");

    private async Task<ProblemRecordedResponse> RecordAsync(RecordProblemRequest? request = null) =>
        (await (await _client.PostAsJsonAsync("/api/v1/problems", request ?? Valid())).Content.ReadFromJsonAsync<ProblemRecordedResponse>())!;

    [Fact]
    public async Task POST_problems_creates_and_returns_201_with_a_location_and_the_identifiers()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/problems", Valid());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProblemRecordedResponse>();
        Assert.NotNull(body);
        Assert.Equal(EncounterTestData.PatientUuid, body.PatientUuid);
        Assert.Equal($"/api/v1/problems/{body.Uuid}", response.Headers.Location?.OriginalString);
        Assert.Equal(1L, await Count("lists", "type = 'medical_problem'"));
    }

    [Fact]
    public async Task POST_problems_with_no_gateway_secret_is_401_and_writes_nothing()
    {
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync("/api/v1/problems", Valid());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0L, await Count("lists"));
    }

    [Fact]
    public async Task POST_problems_for_an_unknown_patient_is_404_problem_json_and_leaves_a_failed_audit_row()
    {
        var unknown = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync("/api/v1/problems", Valid(patient: unknown));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(0L, await Count("lists"));
        Assert.Equal("failed", await fixture.RootScalarAsync<string>("SELECT outcome FROM clinical_care_audit"));
        Assert.Equal("problem.record", await fixture.RootScalarAsync<string>("SELECT action FROM clinical_care_audit"));
        Assert.Equal(unknown.ToString(), await fixture.RootScalarAsync<string>("SELECT CONCAT(subject, '') FROM clinical_care_audit"));
    }

    [Fact]
    public async Task POST_problems_for_the_empty_patient_uuid_is_a_404_not_a_500()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/problems", Valid(patient: Guid.Empty));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(null, "A title is required.")]
    [InlineData("", "A title is required.")]
    [InlineData("a", "Title must be between 2 and 255 characters.")]
    public async Task POST_problems_with_a_missing_or_short_title_is_400(string? title, string expected)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/problems", Valid(title: title));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expected, await ErrorsOf(response));
        Assert.Equal(0L, await Count("lists"));
    }

    [Fact]
    public async Task POST_problems_with_no_begin_date_is_400()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/problems", new { patientUuid = EncounterTestData.PatientUuid, title = "Asthma", providerId = 7 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0L, await Count("lists"));
    }

    [Fact]
    public async Task POST_problems_reports_every_domain_violation_together()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/problems", Valid(diagnosis: "not-a-code", outcome: 9, occurrence: 2, begin: new DateTime(2020, 5, 1), end: new DateTime(2019, 1, 1)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ErrorsOf(response);
        Assert.Contains("Outcome 9 is not a known outcome.", errors);
        Assert.Contains("Occurrence 2 is not a known occurrence.", errors);
        Assert.Contains("End date cannot be before the begin date.", errors);
        Assert.Contains(errors, e => e.StartsWith("Diagnosis must be", StringComparison.Ordinal));
    }

    [Fact]
    public async Task POST_problems_with_a_provider_outside_the_column_range_is_400_not_a_500()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/problems", Valid(providerId: 3_000_000_000));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_problems_accepts_no_diagnosis_and_no_end_date()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/problems", Valid(diagnosis: null, end: null, comments: null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task POST_problems_with_a_zone_converts_to_server_local_time_like_an_encounter_date()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/problems", new
        {
            patientUuid = EncounterTestData.PatientUuid, title = "Asthma", beginDate = "2020-05-01T12:00:00Z", providerId = 7
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var expected = TimeZoneInfo.ConvertTimeFromUtc(new DateTime(2020, 5, 1, 12, 0, 0, DateTimeKind.Utc), TimeZoneInfo.Local);
        Assert.Equal(expected, await fixture.RootScalarAsync<DateTime>("SELECT begdate FROM lists"));
    }

    [Fact]
    public async Task SEC_9_a_recorded_problem_is_audited_with_the_forwarded_address_and_leaves_an_event_and_a_touch_row()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/problems") { Content = JsonContent.Create(Valid()) };
        request.Headers.Add("X-Forwarded-For", "203.0.113.9, 10.0.0.2");

        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<ProblemRecordedResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("success", await fixture.RootScalarAsync<string>("SELECT outcome FROM clinical_care_audit"));
        Assert.Equal(body!.Uuid.ToString(), await fixture.RootScalarAsync<string>("SELECT CONCAT(subject, '') FROM clinical_care_audit"));
        Assert.Equal("203.0.113.9", await fixture.RootScalarAsync<string>("SELECT source_address FROM clinical_care_audit"));
        Assert.Equal(1L, await Count("clinical_care_outbox", "event_type = 'ProblemRecorded'"));
        Assert.Equal(1L, await Count("lists_touch", "type = 'medical_problem'"));
    }

    [Fact]
    public async Task GET_problem_returns_what_POST_recorded_with_a_strong_etag_that_is_the_rows_stamp()
    {
        var created = await RecordAsync(Valid(title: "Asthma", end: new DateTime(2021, 1, 2), outcome: 1, occurrence: 4, referredBy: "Dr Smith"));
        await fixture.RootExecuteAsync("UPDATE lists SET modifydate = '2026-01-02 03:04:05'");

        var response = await _client.GetAsync($"/api/v1/problems/{created.Uuid}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal(created.Uuid, body!.Uuid);
        Assert.Equal("Asthma", body.Title);
        Assert.Equal(new DateTime(2020, 5, 1), body.BeginDate);
        Assert.Equal(new DateTime(2021, 1, 2), body.EndDate);
        Assert.Equal("ICD10:J45.909", body.Diagnosis);
        Assert.Equal(1, body.Outcome);
        Assert.Equal(4, body.Occurrence);
        Assert.Equal("Dr Smith", body.ReferredBy);
        Assert.Equal(EncounterTestData.PatientUuid, body.PatientUuid);
        Assert.Equal(ConcurrencyToken.From(new DateTime(2026, 1, 2, 3, 4, 5)).ToETag(), response.Headers.ETag?.ToString());
        Assert.False(response.Headers.ETag!.IsWeak);
    }

    [Fact]
    public async Task The_problems_etag_changes_when_another_writer_edits_the_row()
    {
        var created = await RecordAsync();
        await fixture.RootExecuteAsync("UPDATE lists SET modifydate = '2026-01-02 03:04:05'");
        var before = (await _client.GetAsync($"/api/v1/problems/{created.Uuid}")).Headers.ETag?.ToString();

        await fixture.RootExecuteAsync("UPDATE lists SET comments = 'edited in the legacy screen'");
        var after = (await _client.GetAsync($"/api/v1/problems/{created.Uuid}")).Headers.ETag?.ToString();

        Assert.NotEqual(before, after);
    }

    [Fact]
    public async Task GET_problem_for_an_unknown_uuid_is_404_with_no_etag()
    {
        var response = await _client.GetAsync($"/api/v1/problems/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Null(response.Headers.ETag);
    }

    [Fact]
    public async Task ADR_0070_GET_problem_for_the_uuid_of_an_allergy_a_medication_or_a_surgery_is_404()
    {
        var allergy = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var medication = Guid.Parse("22222222-2222-4222-8222-222222222222");
        var surgery = Guid.Parse("33333333-3333-4333-8333-333333333333");
        await fixture.RootExecuteAsync(
            "INSERT INTO lists (uuid, type, title, pid, activity) VALUES " +
            $"(UNHEX('{allergy:N}'), 'allergy', 'Penicillin', 42, 1), (UNHEX('{medication:N}'), 'medication', 'Aspirin', 42, 1), " +
            $"(UNHEX('{surgery:N}'), 'surgery', 'Appendectomy', 42, 1)");

        foreach (var uuid in new[] { allergy, medication, surgery })
            Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/problems/{uuid}")).StatusCode);
    }

    [Fact]
    public async Task GET_problems_lists_the_patients_problems_newest_first_with_paging_fields()
    {
        await RecordAsync(Valid(title: "Older", begin: new DateTime(2018, 1, 1)));
        await RecordAsync(Valid(title: "Newer", begin: new DateTime(2022, 1, 1)));

        var response = await _client.GetAsync($"/api/v1/problems?patientUuid={EncounterTestData.PatientUuid}&limit=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProblemListResponse>();
        Assert.Equal(2, body!.Total);
        Assert.Equal(1, body.Limit);
        Assert.Equal(0, body.Offset);
        Assert.Equal("Newer", Assert.Single(body.Items).Title);
        Assert.Null(response.Headers.ETag);   // a list has no single version
    }

    [Fact]
    public async Task GET_problems_without_a_patient_is_400_and_never_an_unbounded_list()
    {
        var response = await _client.GetAsync("/api/v1/problems");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("patientUuid is required.", await ErrorsOf(response));
    }

    [Fact]
    public async Task GET_problems_with_an_oversized_page_is_400_before_anything_is_read()
    {
        var response = await _client.GetAsync($"/api/v1/problems?patientUuid={EncounterTestData.PatientUuid}&limit=10000");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GET_problems_for_an_unknown_patient_is_404()
    {
        var response = await _client.GetAsync($"/api/v1/problems?patientUuid={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GET_problems_leaves_out_other_types_other_patients_and_rows_with_no_uuid()
    {
        await RecordAsync(Valid(title: "Asthma"));
        await fixture.RootExecuteAsync(
            "INSERT INTO lists (uuid, type, title, pid, activity) VALUES " +
            "(UNHEX('11111111111141118111111111111111'), 'allergy', 'Penicillin', 42, 1), " +
            "(UNHEX('33333333333343338333333333333333'), 'medical_problem', 'Someone elses', 99, 1), " +
            "(NULL, 'medical_problem', 'No uuid yet', 42, 1)");

        var body = await (await _client.GetAsync($"/api/v1/problems?patientUuid={EncounterTestData.PatientUuid}"))
            .Content.ReadFromJsonAsync<ProblemListResponse>();

        Assert.Equal(1, body!.Total);
        Assert.Equal("Asthma", Assert.Single(body.Items).Title);
    }

    [Fact]
    public async Task The_read_endpoints_need_the_gateway_secret_too()
    {
        using var anonymous = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/v1/problems/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/v1/problems?patientUuid={EncounterTestData.PatientUuid}")).StatusCode);
    }
}
