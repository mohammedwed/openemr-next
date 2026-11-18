using System.Net;
using System.Net.Http.Json;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenEmrNext.ClinicalCare.Api.Contracts.Requests;
using OpenEmrNext.ClinicalCare.Api.Contracts.Responses;
using Task = System.Threading.Tasks.Task;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// Read-only FHIR <c>AllergyIntolerance</c>, <c>Condition</c>, <c>Encounter</c> and vital-sign <c>Observation</c> (M5e, ADR 0079) through the real HTTP pipeline against a real MariaDB: how each stored record maps, what a
/// search returns and refuses, and that every resource served conforms to its US Core profile. Immunization has its own class; the flow they share is tested there.
/// </summary>
[Collection("ClinicalCareContainer")]
public class FhirClinicalResourceTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    private static readonly FhirJsonParser Parser = new();
    private static readonly Guid OtherPatient = Guid.Parse("b1a9c5f0-0000-4000-8000-0000000000bb");

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private Guid _encounter;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await fixture.SeedPatientAsync(EncounterTestData.PatientUuid, EncounterTestData.PatientPid);
        await fixture.SeedPatientAsync(OtherPatient, 99);
        foreach (var o in new[] { "0", "1" })
        {
            await fixture.RootExecuteAsync($"INSERT INTO list_options (list_id, option_id, title, seq, is_default) VALUES ('outcome', '{o}', 't', {o}, 0)");
            await fixture.RootExecuteAsync($"INSERT INTO list_options (list_id, option_id, title, seq, is_default) VALUES ('occurrence', '{o}', 't', {o}, 0)");
        }

        await AllergyTestData.SeedCodeListsAsync(fixture);
        await fixture.RootExecuteAsync("INSERT INTO openemr_postcalendar_categories (pc_catid, pc_constant_id, pc_catname, pc_cattype, pc_active, pc_seq) VALUES (5, 'office_visit', 'Office Visit', 0, 1, 1)");
        await fixture.RootExecuteAsync("UPDATE users SET fname = 'Doctor', lname = 'Who' WHERE id = 7");
        _factory = ApiFactory.Create(fixture);
        _client = _factory.ClientWithSecret();
        _encounter = await NewEncounterAsync(EncounterTestData.PatientUuid);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    // Arranging, through the JSON API

    private async Task<Guid> NewEncounterAsync(Guid patient) =>
        (await (await _client.PostAsJsonAsync("/api/v1/encounters", new CreateEncounterRequest(patient, "AMB", 5, new DateTime(2026, 11, 10, 9, 30, 0), "annual physical", 3, null, 7, null, null)))
            .Content.ReadFromJsonAsync<EncounterCreatedResponse>())!.Uuid;

    private async Task<Guid> NewProblemAsync(Guid? patient = null, DateTime? end = null, string? diagnosis = "ICD10:J45.909;SNOMED-CT:195967001", string? comments = "worse in winter") =>
        (await (await _client.PostAsJsonAsync("/api/v1/problems", new RecordProblemRequest(patient ?? EncounterTestData.PatientUuid, "Asthma", new DateTime(2020, 5, 1), end, diagnosis, 0, 0, null, comments, 7)))
            .Content.ReadFromJsonAsync<ProblemRecordedResponse>())!.Uuid;

    private async Task<Guid> NewAllergyAsync(Guid? patient = null, DateTime? end = null) =>
        (await (await _client.PostAsJsonAsync("/api/v1/allergies", new RecordAllergyRequest(patient ?? EncounterTestData.PatientUuid, "Penicillin", new DateTime(2019, 3, 2), end, "SNOMED-CT:91936005", "hives", "severe", "confirmed", "rash and itching", 7)))
            .Content.ReadFromJsonAsync<AllergyRecordedResponse>())!.Uuid;

    /// <summary>A full set: every measurement the service holds.</summary>
    private async Task<Guid> NewVitalsAsync(Guid? encounter = null) =>
        (await (await _client.PostAsJsonAsync(
                $"/api/v1/encounters/{encounter ?? _encounter}/vitals",
                new RecordVitalsRequest(
                    new QuantityRequest(70m, "kg"), new QuantityRequest(175m, "cm"), new QuantityRequest(98.6m, "degF"), "oral", 120, 80, 64, 16, 98m,
                    new QuantityRequest(85m, "cm"), new QuantityRequest(36m, "cm"), "calm", 7)))
            .Content.ReadFromJsonAsync<VitalsRecordedResponse>())!.Uuid;

    private async Task<(HttpResponseMessage Response, T? Resource)> GetAsync<T>(string url) where T : Resource
    {
        var response = await _client.GetAsync(url);
        return (response, response.StatusCode == HttpStatusCode.OK ? Parser.Parse<T>(await response.Content.ReadAsStringAsync()) : null);
    }

    private async Task<Bundle> SearchAsync(string url)
    {
        var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Parser.Parse<Bundle>(await response.Content.ReadAsStringAsync());
    }

    private async Task<OperationOutcome> RefusedAsync(string url)
    {
        var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        return Parser.Parse<OperationOutcome>(await response.Content.ReadAsStringAsync());
    }

    private static void AssertConforms(Resource resource, string? profile = null)
    {
        var outcome = FhirProfileValidator.Validate(resource, profile ?? resource.Meta.Profile.First());
        var errors = outcome.Issue.Where(i => i.Severity is OperationOutcome.IssueSeverity.Error or OperationOutcome.IssueSeverity.Fatal)
            .Select(i => $"{resource.TypeName}/{resource.Id}: {i.Details?.Text ?? i.Diagnostics} @ {string.Join(",", i.Expression)}").ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }

    private static string Patient(Guid? uuid = null) => $"?patient={uuid ?? EncounterTestData.PatientUuid}";

    // AllergyIntolerance

    [Fact]
    public async Task An_allergy_is_an_active_allergy_intolerance_with_its_substance_reaction_and_severity()
    {
        var id = await NewAllergyAsync();

        var (_, r) = await GetAsync<AllergyIntolerance>($"/fhir/AllergyIntolerance/{id}");

        Assert.Equal(id.ToString(), r!.Id);
        Assert.Equal("active", r.ClinicalStatus.Coding[0].Code);
        Assert.Equal("confirmed", r.VerificationStatus.Coding[0].Code);
        Assert.Equal("http://snomed.info/sct", r.Code.Coding[0].System);
        Assert.Equal("91936005", r.Code.Coding[0].Code);
        Assert.Equal("Penicillin", r.Code.Text);
        Assert.Equal($"Patient/{EncounterTestData.PatientUuid}", r.Patient.Reference);
        Assert.Equal("hives", r.Reaction[0].Manifestation[0].Text);
        Assert.Equal(AllergyIntolerance.AllergyIntoleranceSeverity.Severe, r.Reaction[0].Severity);
        Assert.Equal("rash and itching", Assert.Single(r.Note).Text);
        Assert.Equal("2019-03-02", ((FhirDateTime)r.Onset).Value);
    }

    [Fact]
    public async Task An_allergy_with_an_end_date_is_inactive()
    {
        var id = await NewAllergyAsync(end: new DateTime(2024, 1, 1));

        Assert.Equal("inactive", (await GetAsync<AllergyIntolerance>($"/fhir/AllergyIntolerance/{id}")).Resource!.ClinicalStatus.Coding[0].Code);
    }

    [Fact]
    public async Task An_allergy_carries_its_stamp_as_last_updated_and_its_version_as_the_json_apis_weak_etag()
    {
        var id = await NewAllergyAsync();
        var jsonETag = (await _client.GetAsync($"/api/v1/allergies/{id}")).Headers.ETag!.ToString();

        var (response, r) = await GetAsync<AllergyIntolerance>($"/fhir/AllergyIntolerance/{id}");

        Assert.Equal($"W/{jsonETag}", response.Headers.ETag!.ToString());
        Assert.Equal(jsonETag.Trim('"'), r!.Meta.VersionId);
        Assert.NotNull(r.Meta.LastUpdated);
    }

    [Fact]
    public async Task What_is_served_as_an_allergy_conforms_to_us_core()
    {
        AssertConforms((await GetAsync<AllergyIntolerance>($"/fhir/AllergyIntolerance/{await NewAllergyAsync()}")).Resource!);
    }

    [Fact]
    public async Task An_allergy_search_returns_only_that_patients_and_an_unknown_id_is_a_404()
    {
        var mine = await NewAllergyAsync();
        await NewAllergyAsync(OtherPatient);

        var bundle = await SearchAsync($"/fhir/AllergyIntolerance{Patient()}");

        Assert.Equal(1, bundle.Total);
        Assert.Equal(mine.ToString(), bundle.Entry[0].Resource.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/fhir/AllergyIntolerance/{Guid.NewGuid()}")).StatusCode);
    }

    // Condition

    [Fact]
    public async Task A_problem_is_an_active_problem_list_condition_with_its_codes_onset_and_note()
    {
        var id = await NewProblemAsync();

        var (_, r) = await GetAsync<Condition>($"/fhir/Condition/{id}");

        Assert.Equal(id.ToString(), r!.Id);
        Assert.Equal("active", r.ClinicalStatus.Coding[0].Code);
        Assert.Equal("problem-list-item", r.Category[0].Coding[0].Code);
        Assert.Equal(["http://hl7.org/fhir/sid/icd-10-cm", "http://snomed.info/sct"], r.Code.Coding.Select(c => c.System));
        Assert.Equal(["J45.909", "195967001"], r.Code.Coding.Select(c => c.Code));
        Assert.Equal("Asthma", r.Code.Text);
        Assert.Equal("2020-05-01", ((FhirDateTime)r.Onset).Value);
        Assert.Equal("worse in winter", Assert.Single(r.Note).Text);
        Assert.Equal($"Patient/{EncounterTestData.PatientUuid}", r.Subject.Reference);
        Assert.NotNull(r.Meta.LastUpdated);
    }

    [Fact]
    public async Task A_problem_with_an_end_date_is_resolved_and_abated_that_day()
    {
        var id = await NewProblemAsync(end: new DateTime(2024, 6, 1));

        var r = (await GetAsync<Condition>($"/fhir/Condition/{id}")).Resource!;

        Assert.Equal("resolved", r.ClinicalStatus.Coding[0].Code);
        Assert.Equal("2024-06-01", ((FhirDateTime)r.Abatement).Value);
    }

    [Fact]
    public async Task A_diagnosis_that_is_not_system_and_code_is_left_out_and_the_title_is_still_the_text()
    {
        // The JSON API would refuse this text, but legacy can store anything, so it is written the way legacy could.
        var id = await NewProblemAsync();
        await fixture.RootExecuteAsync($"UPDATE lists SET diagnosis = 'just words;ICD10:' WHERE uuid = UNHEX('{id:N}')");

        var r = (await GetAsync<Condition>($"/fhir/Condition/{id}")).Resource!;

        Assert.Empty(r.Code.Coding);
        Assert.Equal("Asthma", r.Code.Text);
    }

    [Fact]
    public async Task What_is_served_as_a_condition_conforms_to_us_core()
    {
        AssertConforms((await GetAsync<Condition>($"/fhir/Condition/{await NewProblemAsync()}")).Resource!);
        AssertConforms((await GetAsync<Condition>($"/fhir/Condition/{await NewProblemAsync(end: new DateTime(2024, 6, 1))}")).Resource!);
    }

    [Theory]
    [InlineData("problem-list-item")]
    [InlineData("http://terminology.hl7.org/CodeSystem/condition-category|problem-list-item")]
    public async Task The_one_category_there_is_may_be_asked_for_bare_or_with_its_system(string category)
    {
        await NewProblemAsync();

        Assert.Equal(1, (await SearchAsync($"/fhir/Condition{Patient()}&category={Uri.EscapeDataString(category)}")).Total);
    }

    [Fact]
    public async Task Any_other_category_is_refused_not_answered_with_an_empty_set_that_looks_like_none()
    {
        var outcome = await RefusedAsync($"/fhir/Condition{Patient()}&category=encounter-diagnosis");

        Assert.Contains("problem-list-item", outcome.Issue[0].Diagnostics);
    }

    // Encounter

    [Fact]
    public async Task An_encounter_is_a_finished_encounter_with_its_class_type_period_reason_facility_and_provider()
    {
        var (response, r) = await GetAsync<Encounter>($"/fhir/Encounter/{_encounter}");

        Assert.Equal(Encounter.EncounterStatus.Finished, r!.Status);
        Assert.Equal("AMB", r.Class.Code);
        Assert.Equal("http://terminology.hl7.org/CodeSystem/v3-ActCode", r.Class.System);
        Assert.Equal("Office Visit", r.Type[0].Text);
        Assert.StartsWith("2026-11-10T09:30:00", r.Period.Start);
        Assert.Equal("annual physical", r.ReasonCode[0].Text);
        Assert.Equal("Main Clinic", r.ServiceProvider.Display);
        Assert.Equal($"Practitioner/{ClinicalCareContainerFixture.TestUserUuid}", r.Participant[0].Individual.Reference);
        Assert.Equal("Doctor Who", r.Participant[0].Individual.Display);
        Assert.Single(r.Identifier);
        Assert.Equal($"Patient/{EncounterTestData.PatientUuid}", r.Subject.Reference);
        Assert.True(response.Headers.ETag!.IsWeak);
    }

    [Fact]
    public async Task An_encounters_version_is_the_json_apis_own_token()
    {
        var jsonETag = (await _client.GetAsync($"/api/v1/encounters/{_encounter}")).Headers.ETag!.ToString();

        var (response, r) = await GetAsync<Encounter>($"/fhir/Encounter/{_encounter}");

        Assert.Equal($"W/{jsonETag}", response.Headers.ETag!.ToString());
        Assert.Equal(jsonETag.Trim('"'), r!.Meta.VersionId);
    }

    [Fact]
    public async Task What_is_served_as_an_encounter_conforms_to_us_core()
    {
        AssertConforms((await GetAsync<Encounter>($"/fhir/Encounter/{_encounter}")).Resource!);
    }

    [Fact]
    public async Task An_encounter_search_returns_only_that_patients_and_by_id_only_for_the_patient_named()
    {
        var other = await NewEncounterAsync(OtherPatient);

        var mine = await SearchAsync($"/fhir/Encounter{Patient()}");
        var byId = await SearchAsync($"/fhir/Encounter?_id={_encounter}&patient={OtherPatient}");

        Assert.Equal([_encounter.ToString()], mine.Entry.Select(e => e.Resource.Id));
        Assert.Equal(0, byId.Total);
        Assert.DoesNotContain(other.ToString(), mine.Entry.Select(e => e.Resource.Id));
    }

    // Observation

    [Fact]
    public async Task One_vitals_set_is_ten_observations_with_derived_ids_in_a_fixed_order()
    {
        var vitals = await NewVitalsAsync();

        var bundle = await SearchAsync($"/fhir/Observation{Patient()}");

        Assert.Equal(10, bundle.Total);
        Assert.Equal(
            new[] { "blood-pressure", "heart-rate", "respiratory-rate", "body-temperature", "oxygen-saturation", "body-weight", "body-height", "bmi", "waist-circumference", "head-circumference" }
                .Select(s => $"{vitals}-{s}"),
            bundle.Entry.Select(e => e.Resource.Id));
        Assert.All(bundle.Entry, e => Assert.Equal("vital-signs", ((Observation)e.Resource).Category[0].Coding[0].Code));
    }

    [Fact]
    public async Task Each_measurement_has_its_loinc_code_value_and_ucum_unit()
    {
        var vitals = await NewVitalsAsync();
        async Task<Observation> Get(string slug) => (await GetAsync<Observation>($"/fhir/Observation/{vitals}-{slug}")).Resource!;

        var bp = await Get("blood-pressure");
        var weight = await Get("body-weight");
        var temperature = await Get("body-temperature");

        Assert.Equal("85354-9", bp.Code.Coding[0].Code);
        Assert.Equal([120m, 80m], bp.Component.Select(c => ((Quantity)c.Value).Value!.Value));
        Assert.Equal(["8480-6", "8462-4"], bp.Component.Select(c => c.Code.Coding[0].Code));
        Assert.Equal("29463-7", weight.Code.Coding[0].Code);
        Assert.Equal("[lb_av]", ((Quantity)weight.Value).Code);
        Assert.Equal(154.32m, Math.Round(((Quantity)weight.Value).Value!.Value, 2));
        Assert.Equal("[degF]", ((Quantity)temperature.Value).Code);
        Assert.Equal("oral", temperature.Method.Text);
        Assert.Equal("8867-4", (await Get("heart-rate")).Code.Coding[0].Code);
        Assert.Equal(64m, ((Quantity)(await Get("heart-rate")).Value).Value);
        Assert.Equal($"Encounter/{_encounter}", weight.Encounter.Reference);
        Assert.Equal($"Patient/{EncounterTestData.PatientUuid}", weight.Subject.Reference);
        Assert.Equal("calm", Assert.Single(weight.Note).Text);
    }

    [Fact]
    public async Task A_measurement_that_was_not_taken_is_not_an_observation()
    {
        var vitals = (await (await _client.PostAsJsonAsync(
                $"/api/v1/encounters/{_encounter}/vitals", new RecordVitalsRequest(new QuantityRequest(70m, "kg"), null, null, null, null, null, null, null, null, null, null, null, 7)))
            .Content.ReadFromJsonAsync<VitalsRecordedResponse>())!.Uuid;

        var bundle = await SearchAsync($"/fhir/Observation{Patient()}");

        Assert.Equal([$"{vitals}-body-weight"], bundle.Entry.Select(e => e.Resource.Id));
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/fhir/Observation/{vitals}-body-height")).StatusCode);
    }

    [Theory]
    [InlineData("not-an-id")]
    [InlineData("6c0c0c0c-0000-4000-8000-000000000001")]
    [InlineData("6c0c0c0c-0000-4000-8000-000000000001-no-such-measurement")]
    [InlineData("6c0c0c0c-0000-4000-8000-000000000001-body-weight")]
    public async Task An_observation_id_that_is_malformed_or_names_nothing_is_a_404_operation_outcome(string id)
    {
        var response = await _client.GetAsync($"/fhir/Observation/{id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(OperationOutcome.IssueType.NotFound, Parser.Parse<OperationOutcome>(await response.Content.ReadAsStringAsync()).Issue[0].Code);
    }

    [Fact]
    public async Task A_search_counts_and_pages_over_the_observations_not_the_sets()
    {
        await NewVitalsAsync();
        await NewVitalsAsync();

        var first = await SearchAsync($"/fhir/Observation{Patient()}&_count=7");
        var next = first.Link.Single(l => l.Relation == "next").Url;
        var second = await SearchAsync(next[next.IndexOf("/fhir", StringComparison.Ordinal)..]);
        var third = await SearchAsync(second.Link.Single(l => l.Relation == "next").Url[second.Link.Single(l => l.Relation == "next").Url.IndexOf("/fhir", StringComparison.Ordinal)..]);

        Assert.Equal(20, first.Total);
        Assert.Equal([7, 7, 6], new[] { first.Entry.Count, second.Entry.Count, third.Entry.Count });
        Assert.Equal(20, first.Entry.Concat(second.Entry).Concat(third.Entry).Select(e => e.Resource.Id).Distinct().Count());
        Assert.DoesNotContain(third.Link, l => l.Relation == "next");
    }

    [Fact]
    public async Task A_search_by_id_finds_one_measurement_and_only_for_the_patient_named()
    {
        var vitals = await NewVitalsAsync();

        var found = await SearchAsync($"/fhir/Observation?_id={vitals}-bmi");
        var wrong = await SearchAsync($"/fhir/Observation?_id={vitals}-bmi&patient={OtherPatient}");

        Assert.Equal(1, found.Total);
        Assert.Equal(0, wrong.Total);
    }

    [Fact]
    public async Task Another_patients_vitals_and_deleted_vitals_are_never_served()
    {
        await NewVitalsAsync(await NewEncounterAsync(OtherPatient));
        var mine = await NewVitalsAsync();

        Assert.All((await SearchAsync($"/fhir/Observation{Patient()}")).Entry, e => Assert.StartsWith(mine.ToString(), e.Resource.Id));

        await fixture.RootExecuteAsync("UPDATE forms SET deleted = 1 WHERE formdir = 'vitals'");

        Assert.Equal(0, (await SearchAsync($"/fhir/Observation{Patient()}")).Total);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/fhir/Observation/{mine}-body-weight")).StatusCode);
    }

    [Fact]
    public async Task An_observations_version_is_the_vitals_own_token_as_a_weak_etag_and_a_last_updated()
    {
        var vitals = await NewVitalsAsync();
        var jsonETag = (await _client.GetAsync($"/api/v1/encounters/{_encounter}/vitals/{vitals}")).Headers.ETag!.ToString();

        var (response, r) = await GetAsync<Observation>($"/fhir/Observation/{vitals}-body-weight");

        Assert.Equal($"W/{jsonETag}", response.Headers.ETag!.ToString());
        Assert.Equal(jsonETag.Trim('"'), r!.Meta.VersionId);
        Assert.NotNull(r.Meta.LastUpdated);
    }

    [Fact]
    public async Task Every_observation_served_conforms_to_its_us_core_profile()
    {
        await NewVitalsAsync();

        var bundle = await SearchAsync($"/fhir/Observation{Patient()}");

        foreach (var entry in bundle.Entry)
            AssertConforms(entry.Resource);
    }

    [Theory]
    [InlineData("vital-signs")]
    [InlineData("http://terminology.hl7.org/CodeSystem/observation-category|vital-signs")]
    public async Task The_one_observation_category_may_be_asked_for_bare_or_with_its_system(string category)
    {
        await NewVitalsAsync();

        Assert.Equal(10, (await SearchAsync($"/fhir/Observation{Patient()}&category={Uri.EscapeDataString(category)}")).Total);
    }

    [Theory]
    [InlineData("&category=laboratory")]
    [InlineData("&code=29463-7")]
    [InlineData("&date=ge2026-01-01")]
    public async Task What_the_service_will_not_guess_at_is_refused(string extra)
    {
        await RefusedAsync($"/fhir/Observation{Patient()}{extra}");
    }

    // The same refusals for the other three

    [Theory]
    [InlineData("AllergyIntolerance")]
    [InlineData("Condition")]
    [InlineData("Encounter")]
    public async Task A_search_with_nothing_to_search_on_or_a_parameter_that_is_not_supported_is_a_400(string type)
    {
        await RefusedAsync($"/fhir/{type}");
        await RefusedAsync($"/fhir/{type}{Patient()}&date=2026-01-01");
        await RefusedAsync($"/fhir/{type}{Patient()}&_count=0");
    }

    [Theory]
    [InlineData("AllergyIntolerance")]
    [InlineData("Condition")]
    [InlineData("Encounter")]
    [InlineData("Observation")]
    public async Task A_patient_with_nothing_or_that_is_not_known_is_an_empty_searchset(string type)
    {
        Assert.Equal(0, (await SearchAsync($"/fhir/{type}{Patient(Guid.NewGuid())}")).Total);
    }

    [Theory]
    [InlineData("AllergyIntolerance")]
    [InlineData("Condition")]
    [InlineData("Encounter")]
    [InlineData("Observation")]
    public async Task Without_the_gateway_secret_or_a_verified_user_every_route_is_401(string type)
    {
        using var anonymous = _factory.CreateClient();
        using var noUser = _factory.ClientWithSecretOnly();

        foreach (var client in new[] { anonymous, noUser })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/fhir/{type}{Patient()}")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/fhir/{type}/{Guid.NewGuid()}")).StatusCode);
        }
    }
}
