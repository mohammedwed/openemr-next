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
/// The <c>_lastUpdated</c> search (M5e, ADR 0079 #6) on the four resources that have a stamp, through the real HTTP pipeline against a real MariaDB. Each resource is seeded with records at known stamps, and what a query
/// must return is worked out from the stamps by the FHIR rule here, not written as counts, so a boundary mistake cannot hide in a number that happens to agree.
/// </summary>
[Collection("ClinicalCareContainer")]
public class FhirLastUpdatedTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    private static readonly FhirJsonParser Parser = new();
    private static readonly DateTime[] Stamps = [new(2026, 1, 10, 12, 0, 0), new(2026, 2, 15, 8, 0, 0), new(2026, 3, 20, 17, 30, 0)];

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private readonly Dictionary<string, List<(string Id, DateTime Stamp)>> _seeded = [];

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await fixture.SeedPatientAsync(EncounterTestData.PatientUuid, EncounterTestData.PatientPid);
        foreach (var o in new[] { "0", "1" })
        {
            await fixture.RootExecuteAsync($"INSERT INTO list_options (list_id, option_id, title, seq, is_default) VALUES ('outcome', '{o}', 't', {o}, 0)");
            await fixture.RootExecuteAsync($"INSERT INTO list_options (list_id, option_id, title, seq, is_default) VALUES ('occurrence', '{o}', 't', {o}, 0)");
        }

        await AllergyTestData.SeedCodeListsAsync(fixture);
        _factory = ApiFactory.Create(fixture);
        _client = _factory.ClientWithSecret();

        var encounters = new List<(string, DateTime)>();
        var problems = new List<(string, DateTime)>();
        var allergies = new List<(string, DateTime)>();
        var vitals = new List<(string, DateTime)>();
        var anEncounter = Guid.Empty;
        foreach (var stamp in Stamps)
        {
            var encounter = (await (await _client.PostAsJsonAsync("/api/v1/encounters", new CreateEncounterRequest(EncounterTestData.PatientUuid, null, 5, null, "visit", 3, null, 7, null, null)))
                .Content.ReadFromJsonAsync<EncounterCreatedResponse>())!.Uuid;
            anEncounter = encounter;
            await Stamp("form_encounter", "last_update", encounter, stamp);
            encounters.Add((encounter.ToString(), stamp));

            var problem = (await (await _client.PostAsJsonAsync("/api/v1/problems", new RecordProblemRequest(EncounterTestData.PatientUuid, "Asthma", new DateTime(2020, 5, 1), null, null, 0, 0, null, null, 7)))
                .Content.ReadFromJsonAsync<ProblemRecordedResponse>())!.Uuid;
            await Stamp("lists", "modifydate", problem, stamp);
            problems.Add((problem.ToString(), stamp));

            var allergy = (await (await _client.PostAsJsonAsync("/api/v1/allergies", new RecordAllergyRequest(EncounterTestData.PatientUuid, "Penicillin", null, null, null, "hives", "severe", "confirmed", null, 7)))
                .Content.ReadFromJsonAsync<AllergyRecordedResponse>())!.Uuid;
            await Stamp("lists", "modifydate", allergy, stamp);
            allergies.Add((allergy.ToString(), stamp));

            var set = (await (await _client.PostAsJsonAsync($"/api/v1/encounters/{encounter}/vitals", new RecordVitalsRequest(new QuantityRequest(70m, "kg"), null, null, null, 120, 80, 64, null, null, null, null, null, 7)))
                .Content.ReadFromJsonAsync<VitalsRecordedResponse>())!.Uuid;
            await Stamp("form_vitals", "last_updated", set, stamp);
            vitals.Add((set.ToString(), stamp));
        }

        _seeded["Encounter"] = encounters;
        _seeded["Condition"] = problems;
        _seeded["AllergyIntolerance"] = allergies;
        _seeded["Observation"] = vitals;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>An explicit stamp stops the database moving it, so the record has exactly this one.</summary>
    private Task Stamp(string table, string column, Guid uuid, DateTime stamp) =>
        fixture.RootExecuteAsync($"UPDATE {table} SET {column} = '{stamp:yyyy-MM-dd HH:mm:ss}' WHERE uuid = UNHEX('{uuid:N}')");

    private async Task<Bundle> SearchAsync(string type, string query)
    {
        var response = await _client.GetAsync($"/fhir/{type}?patient={EncounterTestData.PatientUuid}{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Parser.Parse<Bundle>(await response.Content.ReadAsStringAsync());
    }

    /// <summary>The records a bundle holds: an Observation is one of up to ten per vitals set, so it is counted by its set.</summary>
    private static List<string> RecordsIn(Bundle bundle, string type) =>
        bundle.Entry.Select(e => type == "Observation" ? e.Resource.Id[..36] : e.Resource.Id).Distinct().Order().ToList();

    private List<string> Expected(string type, Func<DateTime, bool> inside) =>
        _seeded[type].Where(r => inside(r.Stamp)).Select(r => r.Id).Order().ToList();

    private static string Q(params string[] values) => string.Concat(values.Select(v => $"&_lastUpdated={Uri.EscapeDataString(v)}"));

    public static IEnumerable<object[]> Types => [["Encounter"], ["Condition"], ["AllergyIntolerance"], ["Observation"]];

    public static IEnumerable<object[]> Queries()
    {
        // Each query, with the rule it means applied to a stamp. Boundaries are the point: the stamps include 2026-02-15 08:00:00 exactly.
        (string Value, Func<DateTime, bool> Inside)[] cases =
        [
            ("ge2026-02-01", s => s >= new DateTime(2026, 2, 1)),
            ("gt2026-02-15", s => s >= new DateTime(2026, 2, 16)),           // the day itself is not after itself
            ("lt2026-02-01", s => s < new DateTime(2026, 2, 1)),
            ("le2026-02-15", s => s < new DateTime(2026, 2, 16)),           // the day itself is included
            ("2026-02-15", s => s >= new DateTime(2026, 2, 15) && s < new DateTime(2026, 2, 16)),
            ("eq2026-02", s => s >= new DateTime(2026, 2, 1) && s < new DateTime(2026, 3, 1)),
            ("2026", s => s.Year == 2026),
            ("2025", s => s.Year == 2025),
            ("ge2026-02-15T08:00:00", s => s >= new DateTime(2026, 2, 15, 8, 0, 0)),     // exactly equal is in
            ("gt2026-02-15T08:00:00", s => s >= new DateTime(2026, 2, 15, 8, 0, 1)),     // exactly equal is out
            ("le2026-02-15T08:00:00", s => s < new DateTime(2026, 2, 15, 8, 0, 1)),
            ("lt2026-02-15T08:00:00", s => s < new DateTime(2026, 2, 15, 8, 0, 0)),
            ("2026-02-15T08:00", s => s >= new DateTime(2026, 2, 15, 8, 0, 0) && s < new DateTime(2026, 2, 15, 8, 1, 0)),
            ("2026-03-20T17:30:00", s => s == new DateTime(2026, 3, 20, 17, 30, 0)),
        ];

        foreach (var type in new[] { "Encounter", "Condition", "AllergyIntolerance", "Observation" })
            foreach (var (value, inside) in cases)
                yield return [type, value, inside];
    }

    [Theory]
    [MemberData(nameof(Queries))]
    public async Task A_last_updated_search_returns_exactly_the_records_whose_stamp_is_in_the_range_it_means(string type, string value, Func<DateTime, bool> inside)
    {
        var bundle = await SearchAsync(type, Q(value));

        Assert.Equal(Expected(type, inside), RecordsIn(bundle, type));
    }

    [Theory]
    [MemberData(nameof(Types))]
    public async Task Two_parameters_are_a_range(string type)
    {
        var bundle = await SearchAsync(type, Q("ge2026-02-01", "lt2026-03-01"));

        Assert.Equal(Expected(type, s => s >= new DateTime(2026, 2, 1) && s < new DateTime(2026, 3, 1)), RecordsIn(bundle, type));
    }

    [Theory]
    [MemberData(nameof(Types))]
    public async Task A_range_that_can_hold_nothing_is_an_empty_set_not_an_error(string type)
    {
        var bundle = await SearchAsync(type, Q("ge2026-03-01", "lt2026-01-01"));

        Assert.Equal(0, bundle.Total);
        Assert.Empty(bundle.Entry);
    }

    [Theory]
    [MemberData(nameof(Types))]
    public async Task The_total_is_what_matches_the_window_not_what_the_patient_has(string type)
    {
        var all = await SearchAsync(type, "");
        var windowed = await SearchAsync(type, Q("ge2026-03-01"));

        Assert.True(windowed.Total < all.Total);
        Assert.Equal(type == "Observation" ? 3 : 1, windowed.Total);   // March: one record, or the three Observations its vitals give (weight, pulse and blood pressure)
    }

    [Theory]
    [MemberData(nameof(Types))]
    public async Task The_links_keep_the_window_and_paging_through_it_misses_nothing_and_repeats_nothing(string type)
    {
        var url = $"/fhir/{type}?patient={EncounterTestData.PatientUuid}{Q("ge2026-02-01")}&_count=1";
        var ids = new List<string>();
        Bundle bundle;
        do
        {
            var response = await _client.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            bundle = Parser.Parse<Bundle>(await response.Content.ReadAsStringAsync());
            Assert.Contains("_lastUpdated=ge2026-02-01", bundle.Link.Single(l => l.Relation == "self").Url);
            ids.AddRange(bundle.Entry.Select(e => e.Resource.Id));
            var next = bundle.Link.SingleOrDefault(l => l.Relation == "next")?.Url;
            url = next is null ? "" : next[next.IndexOf("/fhir", StringComparison.Ordinal)..];
        }
        while (url != "");

        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Equal(bundle.Total ?? 0, ids.Count);
        Assert.Equal(Expected(type, s => s >= new DateTime(2026, 2, 1)), ids.Select(i => type == "Observation" ? i[..36] : i).Distinct().Order().ToList());
    }

    [Theory]
    [MemberData(nameof(Types))]
    public async Task A_search_by_id_is_also_held_to_the_window(string type)
    {
        var (id, _) = _seeded[type][1];   // the February one
        var idParam = type == "Observation" ? $"{id}-body-weight" : id;

        var inside = await SearchAsync(type, $"&_id={idParam}{Q("ge2026-02-01")}");
        var outside = await SearchAsync(type, $"&_id={idParam}{Q("ge2026-03-01")}");

        Assert.Equal(1, inside.Total);
        Assert.Equal(0, outside.Total);
    }

    [Theory]
    [MemberData(nameof(Types))]
    public async Task A_value_the_service_cannot_read_and_a_third_parameter_are_refused(string type)
    {
        foreach (var query in new[] { Q("ne2026"), Q("yesterday"), Q("2026-02-30"), Q("ge2026-01-01", "lt2026-02-01", "lt2026-03-01"), "&_lastUpdated=" })
        {
            var response = await _client.GetAsync($"/fhir/{type}?patient={EncounterTestData.PatientUuid}{query}");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.StartsWith("application/fhir+json", response.Content.Headers.ContentType!.ToString());
        }
    }

    [Fact]
    public async Task The_stamp_a_search_filters_on_is_the_one_the_resource_serves_as_last_updated()
    {
        var (id, stamp) = _seeded["Condition"][1];

        var response = await _client.GetAsync($"/fhir/Condition/{id}");
        var resource = Parser.Parse<Condition>(await response.Content.ReadAsStringAsync());

        Assert.Equal(stamp, resource.Meta.LastUpdated!.Value.LocalDateTime);
    }

    [Fact]
    public async Task Immunization_has_no_stamp_so_it_still_refuses_the_parameter()
    {
        var response = await _client.GetAsync($"/fhir/Immunization?patient={EncounterTestData.PatientUuid}{Q("ge2026-01-01")}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
