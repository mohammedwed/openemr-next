using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenEmrNext.ClinicalCare.Api.Contracts.Requests;
using OpenEmrNext.ClinicalCare.Api.Contracts.Responses;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// ADR 0075 at the service: every <c>/api/v1</c> route needs the user the gateway verified, resolved to a real, active legacy user, who becomes the acting provider and the audit actor. The body's
/// <c>providerId</c> can no longer say who is acting.
/// </summary>
[Collection("ClinicalCareContainer")]
public class VerifiedUserTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    private static readonly Guid NurseUuid = Guid.Parse("7e57c0de-0000-4000-8000-000000000008");
    private static readonly Guid InactiveUuid = Guid.Parse("7e57c0de-0000-4000-8000-000000000009");

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _doctor = null!;   // user 7, dr_who

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await fixture.RootExecuteAsync("INSERT INTO users (id, username, uuid, active) VALUES (8, 'nurse_joy', UNHEX('" + NurseUuid.ToString("N") + "'), 1), (9, 'left_the_clinic', UNHEX('" + InactiveUuid.ToString("N") + "'), 0)");
        await fixture.RootExecuteAsync("INSERT INTO `groups` (name, `user`) VALUES ('Nurses', 'nurse_joy')");
        await fixture.SeedPatientAsync(EncounterTestData.PatientUuid, EncounterTestData.PatientPid);
        _factory = ApiFactory.Create(fixture);
        _doctor = _factory.ClientWithSecret();
    }

    public Task DisposeAsync()
    {
        _doctor.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private static CreateEncounterRequest NewEncounter(long? provider = null) =>
        new(EncounterTestData.PatientUuid, null, 5, null, "visit", 3, null, provider, null, null);

    private Task<long> Count(string table, string where = "1=1") => fixture.RootScalarAsync<long>($"SELECT COUNT(*) FROM {table} WHERE {where}");

    private async Task<Guid> EncounterAsync(HttpClient client) =>
        (await (await client.PostAsJsonAsync("/api/v1/encounters", NewEncounter())).Content.ReadFromJsonAsync<EncounterCreatedResponse>())!.Uuid;

    // Who is acting

    public static IEnumerable<object[]> Routes =>
    [
        ["GET", "/api/v1/encounters/6c0c0c0c-0000-4000-8000-000000000001"],
        ["GET", "/api/v1/encounters/6c0c0c0c-0000-4000-8000-000000000001/vitals"],
        ["GET", "/api/v1/encounters/6c0c0c0c-0000-4000-8000-000000000001/soap-notes"],
        ["GET", "/api/v1/encounters/6c0c0c0c-0000-4000-8000-000000000001/clinical-notes"],
        ["GET", "/api/v1/problems"], ["GET", "/api/v1/allergies"], ["GET", "/api/v1/immunizations"],
        ["GET", "/api/v1/lookups/facilities"], ["GET", "/api/v1/lookups/immunization-options"], ["POST", "/api/v1/encounters"],
        ["PUT", "/api/v1/problems/6c0c0c0c-0000-4000-8000-000000000001"]
    ];

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task A_request_with_the_secret_but_no_verified_user_is_401_on_every_route_whatever_else_is_wrong_with_it(string method, string path)
    {
        using var client = _factory.ClientWithSecretOnly();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method != "GET")
            request.Content = JsonContent.Create(new { });   // invalid on purpose: a 400 here would mean the body was judged before the caller was

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0L, await Count("form_encounter"));
        Assert.Equal(0L, await Count("clinical_care_audit"));
    }

    [Theory]
    [InlineData("not-a-uuid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("")]
    public async Task A_malformed_or_empty_identity_is_401(string header)
    {
        using var client = _factory.ClientWithSecretOnly();
        client.DefaultRequestHeaders.Add(ApiFactory.UserHeader, header);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/problems?patientUuid=" + EncounterTestData.PatientUuid)).StatusCode);
    }

    [Fact]
    public async Task A_uuid_that_matches_no_user_is_403_and_nothing_is_written()
    {
        using var stranger = _factory.ClientWithSecret(Guid.NewGuid());

        var response = await stranger.PostAsJsonAsync("/api/v1/encounters", NewEncounter());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0L, await Count("form_encounter"));
        Assert.Equal(0L, await Count("clinical_care_audit"));
    }

    [Fact]
    public async Task An_account_legacy_has_deactivated_is_403()
    {
        using var gone = _factory.ClientWithSecret(InactiveUuid);

        var response = await gone.PostAsJsonAsync("/api/v1/encounters", NewEncounter());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0L, await Count("form_encounter"));
    }

    [Fact]
    public async Task The_shared_secret_is_still_required_and_a_verified_user_alone_is_not_enough()
    {
        using var noSecret = _factory.CreateClient();
        noSecret.DefaultRequestHeaders.Add(ApiFactory.UserHeader, ClinicalCareContainerFixture.TestUserUuid.ToString());

        Assert.Equal(HttpStatusCode.Unauthorized, (await noSecret.GetAsync("/api/v1/problems?patientUuid=" + EncounterTestData.PatientUuid)).StatusCode);
    }

    [Fact]
    public async Task The_event_delivery_endpoint_is_service_to_service_and_asks_for_no_user()
    {
        using var client = _factory.CreateClient();   // no secret, no user

        var response = await client.PostAsJsonAsync("/internal/events", new { });

        // Not the sign-in answer: it has its own secret (ADR 0066), which this request lacks, so it is refused for that, and it never asks who the user is.
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.NotFound });
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // What the verified user becomes

    [Fact]
    public async Task A_record_is_audited_as_the_verified_user_and_says_so_and_its_event_names_them_too()
    {
        var uuid = await EncounterAsync(_doctor);

        Assert.Equal(ClinicalCareContainerFixture.TestUserUuid.ToString(), await fixture.RootScalarAsync<string>("SELECT actor FROM clinical_care_audit WHERE action = 'encounter.create'"));
        Assert.Equal(7L, await fixture.RootScalarAsync<long>("SELECT actor_id FROM clinical_care_audit WHERE action = 'encounter.create'"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT actor_verified FROM clinical_care_audit WHERE action = 'encounter.create'"));
        Assert.Contains(ClinicalCareContainerFixture.TestUserUuid.ToString(), await fixture.RootScalarAsync<string>("SELECT payload FROM clinical_care_outbox WHERE event_type = 'EncounterCreated'"));
        Assert.NotEqual(Guid.Empty, uuid);
    }

    [Fact]
    public async Task A_correction_and_a_refusal_are_audited_as_the_verified_user_not_as_unknown()
    {
        var encounter = await EncounterAsync(_doctor);
        var recorded = await (await _doctor.PostAsJsonAsync($"/api/v1/encounters/{encounter}/soap-notes", new RecordSoapNoteRequest("cough", null, null, null, null))).Content.ReadFromJsonAsync<SoapNoteRecordedResponse>();
        var tag = (await _doctor.GetAsync($"/api/v1/encounters/{encounter}/soap-notes/{recorded!.Id}")).Headers.ETag!.ToString();
        using var put = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/encounters/{encounter}/soap-notes/{recorded.Id}") { Content = JsonContent.Create(new CorrectSoapNoteRequest("cough", "clear", null, null)) };
        put.Headers.TryAddWithoutValidation("If-Match", tag);
        using var stale = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/encounters/{encounter}/soap-notes/{recorded.Id}") { Content = JsonContent.Create(new CorrectSoapNoteRequest("x", "y", null, null)) };
        stale.Headers.TryAddWithoutValidation("If-Match", "\"c-00000000000000000000000000000000\"");

        await _doctor.SendAsync(put);
        await _doctor.SendAsync(stale);

        Assert.Equal(0L, await Count("clinical_care_audit", "actor = 'unknown'"));
        Assert.Equal(1L, await Count("clinical_care_audit", "action = 'soap.correct' AND outcome = 'success' AND actor_verified = 1 AND actor_id = 7"));
        Assert.Equal(1L, await Count("clinical_care_audit", "action = 'soap.correct' AND outcome = 'failed' AND actor_verified = 1 AND actor_id = 7"));
    }

    [Fact]
    public async Task A_different_signed_in_user_is_a_different_actor()
    {
        using var nurse = _factory.ClientWithSecret(NurseUuid);

        await EncounterAsync(nurse);

        Assert.Equal(8L, await fixture.RootScalarAsync<long>("SELECT actor_id FROM clinical_care_audit WHERE action = 'encounter.create'"));
        Assert.Equal(NurseUuid.ToString(), await fixture.RootScalarAsync<string>("SELECT actor FROM clinical_care_audit WHERE action = 'encounter.create'"));
    }

    [Fact]
    public async Task The_body_cannot_name_who_is_acting_the_verified_user_is_recorded_as_the_author_whatever_providerId_says()
    {
        var encounter = await EncounterAsync(_doctor);
        using var nurse = _factory.ClientWithSecret(NurseUuid);

        // The nurse is signed in; the body claims to be the doctor (7). Everything written names the nurse.
        await nurse.PostAsJsonAsync($"/api/v1/encounters/{encounter}/soap-notes", new RecordSoapNoteRequest("cough", null, null, null, ProviderId: 7));
        await nurse.PostAsJsonAsync($"/api/v1/encounters/{encounter}/vitals", new RecordVitalsRequest(null, null, null, null, 120, 80, 64, null, null, null, null, null, ProviderId: 7));
        var immunization = await nurse.PostAsJsonAsync(
            "/api/v1/immunizations", new RecordImmunizationRequest(
                EncounterTestData.PatientUuid, DateTime.Now.AddDays(-1), null, "140", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, false, ProviderId: 7));

        Assert.Equal(HttpStatusCode.Created, immunization.StatusCode);
        Assert.Equal("nurse_joy", await fixture.RootScalarAsync<string>("SELECT `user` FROM form_soap"));
        Assert.Equal("Nurses", await fixture.RootScalarAsync<string>("SELECT groupname FROM form_soap"));
        Assert.Equal("nurse_joy", await fixture.RootScalarAsync<string>("SELECT `user` FROM form_vitals"));
        Assert.Equal(8L, await fixture.RootScalarAsync<long>("SELECT created_by FROM immunizations"));
        Assert.Equal(0L, await Count("clinical_care_audit", "action IN ('soap.record', 'vitals.record', 'immunization.record') AND actor_id <> 8"));
    }

    [Fact]
    public async Task The_provider_id_can_be_left_out_of_every_record_request()
    {
        var encounter = await EncounterAsync(_doctor);
        var problem = await _doctor.PostAsJsonAsync(
            "/api/v1/problems", new RecordProblemRequest(EncounterTestData.PatientUuid, "Asthma", DateTime.Now.AddDays(-30), null, null, 0, 0, null, null, null));
        var allergy = await _doctor.PostAsJsonAsync(
            "/api/v1/allergies", new RecordAllergyRequest(EncounterTestData.PatientUuid, "Penicillin", DateTime.Now.AddDays(-30), null, null, null, null, null, null, ProviderId: null));
        var note = await _doctor.PostAsJsonAsync($"/api/v1/encounters/{encounter}/clinical-notes", new { type = "progress_note", description = "stable" });

        Assert.Equal(HttpStatusCode.Created, problem.StatusCode);
        Assert.Equal(HttpStatusCode.Created, allergy.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, note.StatusCode);   // the type is not seeded here, so it may be a 400; the point is the missing providerId is not the reason
        Assert.DoesNotContain("ProviderId", await note.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task An_encounters_own_provider_defaults_to_the_acting_user_and_can_differ_from_them()
    {
        using var nurse = _factory.ClientWithSecret(NurseUuid);

        var defaulted = await EncounterAsync(nurse);
        await nurse.PostAsJsonAsync("/api/v1/encounters", NewEncounter(provider: 7));   // the visit is the doctor's; the nurse is the one recording it

        Assert.Equal(8L, await fixture.RootScalarAsync<long>($"SELECT provider_id FROM form_encounter WHERE uuid = UNHEX('{defaulted:N}')"));
        Assert.Equal(1L, await Count("form_encounter", "provider_id = 7"));
        Assert.Equal(2L, await Count("clinical_care_audit", "action = 'encounter.create' AND actor_id = 8"));   // both recordings are the nurse's, whoever the visit is for
    }
}
