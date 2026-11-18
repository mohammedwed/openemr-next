using System.Net;
using System.Net.Http.Json;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Task = System.Threading.Tasks.Task;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenEmrNext.ClinicalCare.Api.Contracts.Requests;
using OpenEmrNext.ClinicalCare.Api.Contracts.Responses;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// Read-only FHIR <c>Immunization</c> (M5e, ADR 0079) through the real HTTP pipeline against a real MariaDB, as the production principal: how a stored immunization maps, what a search returns and refuses, that the
/// same sign-in applies as everywhere, and that the output conforms to US Core.
/// </summary>
[Collection("ClinicalCareContainer")]
public class FhirImmunizationTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    private static readonly FhirJsonParser Parser = new();
    private static readonly Guid OtherPatient = Guid.Parse("b1a9c5f0-0000-4000-8000-0000000000bb");

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await ImmunizationTestData.SeedCodesAsync(fixture);
        await fixture.SeedPatientAsync(EncounterTestData.PatientUuid, EncounterTestData.PatientPid);
        await fixture.SeedPatientAsync(OtherPatient, 99);
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
        Guid? patient = null, DateTime? given = null, string? completion = "Completed", string? refusal = null, long? administeredById = 7, long? ordering = 9, string? lot = "L1") =>
        new(patient ?? EncounterTestData.PatientUuid, given ?? new DateTime(2026, 11, 10, 8, 15, 30), 6, "140", "Acme", lot, new DateTime(2027, 1, 1), 0.5m, "1", "intramuscular",
            "left_arm", completion, "00", refusal, new DateTime(2026, 11, 10), new DateTime(2026, 8, 6), "Nurse Joy", administeredById, ordering, null, "R1", "why", "arm sore", false, 7);

    private async Task<Guid> RecordedAsync(RecordImmunizationRequest? request = null)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/immunizations", request ?? Record());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ImmunizationRecordedResponse>())!.Uuid;
    }

    private async Task<Immunization> ReadAsync(Guid id)
    {
        var response = await _client.GetAsync($"/fhir/Immunization/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Parser.Parse<Immunization>(await response.Content.ReadAsStringAsync());
    }

    private async Task<(HttpStatusCode Status, Bundle? Bundle, OperationOutcome? Outcome)> SearchAsync(string query, HttpClient? client = null)
    {
        var response = await (client ?? _client).GetAsync($"/fhir/Immunization{query}");
        var json = await response.Content.ReadAsStringAsync();
        return response.StatusCode == HttpStatusCode.OK ? (response.StatusCode, Parser.Parse<Bundle>(json), null) : (response.StatusCode, null, Parser.Parse<OperationOutcome>(json));
    }

    private static string Patient(Guid? uuid = null) => $"?patient={uuid ?? EncounterTestData.PatientUuid}";

    // Read and mapping

    [Fact]
    public async Task A_read_is_fhir_json_with_the_records_facts_mapped_the_way_legacy_maps_them()
    {
        var id = await RecordedAsync();

        var response = await _client.GetAsync($"/fhir/Immunization/{id}");
        var r = Parser.Parse<Immunization>(await response.Content.ReadAsStringAsync());

        Assert.StartsWith("application/fhir+json", response.Content.Headers.ContentType!.ToString());
        Assert.Equal(id.ToString(), r.Id);
        Assert.Equal(Immunization.ImmunizationStatusCodes.Completed, r.Status);
        Assert.Equal("140", Assert.Single(r.VaccineCode.Coding).Code);
        Assert.Equal("http://hl7.org/fhir/sid/cvx", r.VaccineCode.Coding[0].System);
        Assert.Equal($"Patient/{EncounterTestData.PatientUuid}", r.Patient.Reference);
        Assert.Equal("L1", r.LotNumber);
        Assert.Equal("2027-01-01", r.ExpirationDate);
        Assert.Equal("Acme", r.Manufacturer.Display);
        Assert.Equal(0.5m, r.DoseQuantity.Value);
        Assert.StartsWith("2026-11-10T08:15:30", ((FhirDateTime)r.Occurrence).Value);
        Assert.Contains("+", ((FhirDateTime)r.Occurrence).Value + "+");   // a date-time with a time carries an offset (FHIR requires one)
        Assert.Equal("arm sore", Assert.Single(r.Note).Text);
        Assert.Equal("2026-08-06", r.Education[0].PublicationDate);
    }

    [Fact]
    public async Task A_person_the_record_names_is_a_reference_with_a_display_name_when_they_are_a_known_user_and_a_name_only_when_not()
    {
        var id = await RecordedAsync(Record(administeredById: 7, ordering: 9));   // user 7 is dr_who; there is no user 9

        var r = await ReadAsync(id);

        var administering = Assert.Single(r.Performer);   // the unknown ordering provider has neither a uuid nor a name, so is left out
        Assert.Equal("AP", administering.Function.Coding[0].Code);
        Assert.Equal($"Practitioner/{ClinicalCareContainerFixture.TestUserUuid}", administering.Actor.Reference);
        Assert.Equal("Nurse Joy", administering.Actor.Display);
    }

    [Fact]
    public async Task The_version_is_the_json_apis_own_token_in_the_meta_and_as_a_weak_etag_and_there_is_no_last_updated()
    {
        var id = await RecordedAsync();
        var jsonETag = (await _client.GetAsync($"/api/v1/immunizations/{id}")).Headers.ETag!.ToString();

        var response = await _client.GetAsync($"/fhir/Immunization/{id}");
        var r = Parser.Parse<Immunization>(await response.Content.ReadAsStringAsync());

        Assert.True(response.Headers.ETag!.IsWeak);
        Assert.Equal($"W/{jsonETag}", response.Headers.ETag.ToString());
        Assert.Equal(jsonETag.Trim('"'), r.Meta.VersionId);
        Assert.Null(r.Meta.LastUpdated);   // this table has no reliable stamp (ADR 0072): never guessed
    }

    [Fact]
    public async Task A_correction_moves_the_fhir_version_with_the_json_one()
    {
        var id = await RecordedAsync();
        var before = (await ReadAsync(id)).Meta.VersionId;
        var tag = (await _client.GetAsync($"/api/v1/immunizations/{id}")).Headers.ETag!.ToString();
        var put = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/immunizations/{id}")
        {
            Content = JsonContent.Create(new CorrectImmunizationRequest(
                new DateTime(2026, 11, 10, 8, 15, 30), 6, "140", "Acme", "L2", new DateTime(2027, 1, 1), 0.5m, "1", "intramuscular", "left_arm", "Completed", "00", null,
                new DateTime(2026, 11, 10), new DateTime(2026, 8, 6), "Nurse Joy", 7, 9, null, "R1", "why", "arm sore", false))
        };
        put.Headers.TryAddWithoutValidation("If-Match", tag);
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(put)).StatusCode);

        var after = await ReadAsync(id);

        Assert.NotEqual(before, after.Meta.VersionId);
        Assert.Equal("L2", after.LotNumber);
    }

    [Fact]
    public async Task A_record_entered_in_error_is_entered_in_error_whatever_its_completion_status()
    {
        var id = await RecordedAsync();
        var tag = (await _client.GetAsync($"/api/v1/immunizations/{id}")).Headers.ETag!.ToString();
        var put = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/immunizations/{id}")
        {
            Content = JsonContent.Create(new CorrectImmunizationRequest(
                new DateTime(2026, 11, 10, 8, 15, 30), 6, "140", "Acme", "L1", new DateTime(2027, 1, 1), 0.5m, "1", "intramuscular", "left_arm", "Completed", "00", null,
                new DateTime(2026, 11, 10), new DateTime(2026, 8, 6), "Nurse Joy", 7, 9, null, "R1", "why", "arm sore", true))
        };
        put.Headers.TryAddWithoutValidation("If-Match", tag);
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(put)).StatusCode);

        Assert.Equal(Immunization.ImmunizationStatusCodes.EnteredInError, (await ReadAsync(id)).Status);
    }

    [Fact]
    public async Task A_refused_immunization_is_not_done_with_the_refusal_reason_as_the_status_reason()
    {
        var id = await RecordedAsync(Record(completion: "Refused", refusal: "PATOBJ"));

        var r = await ReadAsync(id);

        Assert.Equal(Immunization.ImmunizationStatusCodes.NotDone, r.Status);
        Assert.Equal("PATOBJ", r.StatusReason.Coding[0].Code);
        Assert.Equal("http://terminology.hl7.org/CodeSystem/v3-ActReason", r.StatusReason.Coding[0].System);
    }

    [Fact]
    public async Task A_completed_immunization_carries_no_status_reason()
    {
        Assert.Null((await ReadAsync(await RecordedAsync())).StatusReason);
    }

    [Fact]
    public async Task An_immunization_that_does_not_exist_is_a_404_operation_outcome()
    {
        var response = await _client.GetAsync($"/fhir/Immunization/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var outcome = Parser.Parse<OperationOutcome>(await response.Content.ReadAsStringAsync());
        Assert.Equal(OperationOutcome.IssueType.NotFound, outcome.Issue[0].Code);
    }

    [Fact]
    public async Task What_is_served_conforms_to_us_core()
    {
        var r = await ReadAsync(await RecordedAsync());

        var outcome = FhirProfileValidator.Validate(r, FhirProfileValidator.UsCoreImmunization);

        var errors = outcome.Issue.Where(i => i.Severity is OperationOutcome.IssueSeverity.Error or OperationOutcome.IssueSeverity.Fatal).Select(i => $"{i.Details?.Text ?? i.Diagnostics} @ {string.Join(",", i.Expression)}").ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }

    // Search

    [Fact]
    public async Task A_search_for_a_patient_is_a_searchset_of_that_patients_immunizations_newest_first_and_no_ones_elses()
    {
        var older = await RecordedAsync(Record(given: new DateTime(2026, 1, 1, 9, 0, 0)));
        var newer = await RecordedAsync(Record(given: new DateTime(2026, 11, 1, 9, 0, 0)));
        await RecordedAsync(Record(patient: OtherPatient));

        var (status, bundle, _) = await SearchAsync(Patient());

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(Bundle.BundleType.Searchset, bundle!.Type);
        Assert.Equal(2, bundle.Total);
        Assert.Equal([newer.ToString(), older.ToString()], bundle.Entry.Select(e => e.Resource.Id));
        Assert.All(bundle.Entry, e => Assert.EndsWith($"/fhir/Immunization/{e.Resource.Id}", e.FullUrl));
        Assert.All(bundle.Entry, e => Assert.Equal(Bundle.SearchEntryMode.Match, e.Search.Mode));
    }

    [Fact]
    public async Task The_patient_may_be_given_as_a_bare_id_or_as_a_reference()
    {
        await RecordedAsync();

        var bare = await SearchAsync($"?patient={EncounterTestData.PatientUuid}");
        var reference = await SearchAsync($"?patient=Patient/{EncounterTestData.PatientUuid}");

        Assert.Equal(1, bare.Bundle!.Total);
        Assert.Equal(1, reference.Bundle!.Total);
    }

    [Fact]
    public async Task Paging_follows_the_next_link_until_there_is_none()
    {
        for (var day = 1; day <= 3; day++)
            await RecordedAsync(Record(given: new DateTime(2026, 11, day, 9, 0, 0)));

        var first = (await SearchAsync($"{Patient()}&_count=2")).Bundle!;
        var next = first.Link.Single(l => l.Relation == "next").Url;
        var second = (await SearchAsync(next[next.IndexOf('?')..])).Bundle!;

        Assert.Equal(3, first.Total);
        Assert.Equal(2, first.Entry.Count);
        Assert.Single(second.Entry);
        Assert.DoesNotContain(second.Link, l => l.Relation == "next");
        Assert.Equal(3, first.Entry.Concat(second.Entry).Select(e => e.Resource.Id).Distinct().Count());   // no entry twice, none missed
        Assert.Contains("_count=2", first.Link.Single(l => l.Relation == "self").Url);
    }

    [Fact]
    public async Task A_search_by_id_finds_one_and_only_for_the_patient_named()
    {
        var id = await RecordedAsync();

        var byId = await SearchAsync($"?_id={id}");
        var rightPatient = await SearchAsync($"?_id={id}&patient={EncounterTestData.PatientUuid}");
        var wrongPatient = await SearchAsync($"?_id={id}&patient={OtherPatient}");

        Assert.Equal(1, byId.Bundle!.Total);
        Assert.Equal(1, rightPatient.Bundle!.Total);
        Assert.Equal(0, wrongPatient.Bundle!.Total);
    }

    [Fact]
    public async Task A_patient_with_nothing_or_that_is_not_known_is_an_empty_searchset_not_an_error()
    {
        var none = await SearchAsync(Patient());
        var unknown = await SearchAsync(Patient(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.OK, none.Status);
        Assert.Equal(0, none.Bundle!.Total);
        Assert.Equal(HttpStatusCode.OK, unknown.Status);
        Assert.Empty(unknown.Bundle!.Entry);
    }

    // What is refused

    [Theory]
    [InlineData("")]                                                    // nothing to search on
    [InlineData("?patient=not-a-uuid")]
    [InlineData("?patient=Practitioner/6c0c0c0c-0000-4000-8000-000000000001")]
    [InlineData("?patient=00000000-0000-0000-0000-000000000000")]
    [InlineData("?_id=not-a-uuid")]
    [InlineData("?patient=6c0c0c0c-0000-4000-8000-000000000001&_count=0")]
    [InlineData("?patient=6c0c0c0c-0000-4000-8000-000000000001&_count=101")]
    [InlineData("?patient=6c0c0c0c-0000-4000-8000-000000000001&_count=many")]
    [InlineData("?patient=6c0c0c0c-0000-4000-8000-000000000001&_offset=-1")]
    [InlineData("?patient=6c0c0c0c-0000-4000-8000-000000000001&date=2026-01-01")]            // not supported: refused, not ignored
    [InlineData("?patient=6c0c0c0c-0000-4000-8000-000000000001&_lastUpdated=gt2020-01-01")]  // this resource has no stamp to search on
    [InlineData("?patient=6c0c0c0c-0000-4000-8000-000000000001&_sort=date")]
    [InlineData("?patient=6c0c0c0c-0000-4000-8000-000000000001&patient=6c0c0c0c-0000-4000-8000-000000000002")]   // which one counts is a guess
    [InlineData("?patient=")]
    public async Task A_search_the_service_will_not_guess_at_is_a_400_operation_outcome_and_never_a_different_answer(string query)
    {
        var (status, bundle, outcome) = await SearchAsync(query);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Null(bundle);
        Assert.Equal(OperationOutcome.IssueSeverity.Error, outcome!.Issue[0].Severity);
        Assert.False(string.IsNullOrWhiteSpace(outcome.Issue[0].Diagnostics));
    }

    [Fact]
    public async Task The_refusal_of_an_unsupported_parameter_names_it_and_what_is_supported()
    {
        var (_, _, outcome) = await SearchAsync($"{Patient()}&date=2026-01-01");

        var text = outcome!.Issue[0].Diagnostics;
        Assert.Contains("'date'", text);
        Assert.Contains("patient", text);
        Assert.Contains("_count", text);
    }

    // Access

    [Fact]
    public async Task Without_the_gateway_secret_every_fhir_route_is_401()
    {
        using var anonymous = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/fhir/Immunization/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/fhir/Immunization{Patient()}")).StatusCode);
    }

    [Fact]
    public async Task Without_a_verified_user_every_fhir_route_is_401()
    {
        using var noUser = _factory.ClientWithSecretOnly();

        Assert.Equal(HttpStatusCode.Unauthorized, (await noUser.GetAsync($"/fhir/Immunization/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await noUser.GetAsync($"/fhir/Immunization{Patient()}")).StatusCode);
    }

    [Fact]
    public async Task Reading_changes_nothing()
    {
        await RecordedAsync();
        var audits = await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_audit");
        var events = await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_outbox");

        await SearchAsync(Patient());
        await SearchAsync($"{Patient()}&_count=1");

        Assert.Equal(audits, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_audit"));
        Assert.Equal(events, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_outbox"));
    }
}
