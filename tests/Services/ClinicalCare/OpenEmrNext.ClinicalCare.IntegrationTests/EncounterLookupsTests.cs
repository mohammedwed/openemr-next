using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenEmrNext.ClinicalCare.Api.Contracts.Requests;
using OpenEmrNext.ClinicalCare.Api.Contracts.Responses;
using OpenEmrNext.ClinicalCare.Infrastructure;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// The encounter lookups (M5d slice 4) against a real MariaDB as the production principal, through the real HTTP pipeline: the right rows from legacy's configuration, in legacy's order, and nothing a caller has no need
/// to see. Each choice a caller makes from them is one the encounter endpoints accept.
/// </summary>
[Collection("ClinicalCareContainer")]
public class EncounterLookupsTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();   // seeds two facilities (3 'Main Clinic' with POS 11, 4 'Unnamed POS') and three classes, one default
        await fixture.RootExecuteAsync(
            """
            INSERT INTO openemr_postcalendar_categories (pc_catid, pc_constant_id, pc_catname, pc_cattype, pc_active, pc_seq) VALUES
                (5, 'office_visit', 'Office Visit', 0, 1, 20),
                (1, 'no_show', 'No Show', 0, 1, 10),
                (9, 'established_patient', 'Established Patient', 0, 1, 10),
                (2, 'in_office', 'In Office', 1, 1, 5),
                (6, 'holidays', 'Holidays', 2, 1, 5),
                (12, 'therapy', 'Group Therapy', 3, 1, 5),
                (13, 'retired', 'Retired Visit', 0, 0, 1),
                (14, NULL, 'No Constant', 0, 1, 30)
            """);
        await fixture.RootExecuteAsync("INSERT INTO facility (id, name, pos_code, inactive, service_location) VALUES (5, 'Closed Annex', 21, 1, 1), (6, 'Alpha Lab', 81, 0, 0)");
        await fixture.RootExecuteAsync(
            """
            INSERT INTO users (id, username, fname, lname, authorized, active) VALUES
                (20, 'zed', 'Zed', 'Zimmer', 1, 1),
                (21, 'ann', 'Ann', 'Adams', 1, 1),
                (22, 'abe', 'Abe', 'Adams', 1, 1),
                (23, 'left', 'Lee', 'Left', 1, 0),
                (24, 'clerk', 'Cora', 'Clerk', 0, 1),
                (25, 'noname', NULL, NULL, 1, 1),
                (26, '', 'Blank', 'Login', 1, 1),
                (27, 'solo', 'Solo', NULL, 1, 1)
            """);
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

    [Fact]
    public async Task Classes_come_back_in_legacys_order_with_the_default_marked()
    {
        var classes = await _client.GetFromJsonAsync<List<EncounterClassLookupResponse>>("/api/v1/lookups/encounter-classes");

        Assert.Equal(["AMB", "EMER", "ACUTE"], classes!.Select(c => c.Code));   // by seq: 10, 20, 60
        Assert.Equal("Outpatient", classes![0].Title);
        Assert.Equal([true, false, false], classes.Select(c => c.IsDefault));
    }

    [Fact]
    public async Task Facilities_are_the_active_ones_by_name_with_their_place_of_service()
    {
        var facilities = await _client.GetFromJsonAsync<List<FacilityLookupResponse>>("/api/v1/lookups/facilities");

        Assert.Equal(["Alpha Lab", "Main Clinic", "Unnamed POS"], facilities!.Select(f => f.Name));   // the inactive 'Closed Annex' is left out
        Assert.Equal([6, 3, 4], facilities!.Select(f => f.Id));
        Assert.Equal([81, 11, (int?)null], facilities!.Select(f => f.PosCode));
        // Legacy's schema defaults service_location to 1, so only the facility seeded with 0 is not one.
        Assert.Equal([false, true, true], facilities!.Select(f => f.IsServiceLocation));
    }

    [Fact]
    public async Task Providers_are_the_active_authorized_users_by_last_name_then_first_with_a_name_to_show()
    {
        var providers = await _client.GetFromJsonAsync<List<ProviderLookupResponse>>("/api/v1/lookups/providers");

        // By last name then first name then id, with a missing name sorting first (as MariaDB orders NULL): the user with no name (shown by username), the one with only a first name, then Adams Abe,
        // Adams Ann and Zimmer Zed. Left out: the inactive user, the one legacy has not authorized, and the one with no username (not a usable account). The seeded dr_who (not authorized) is not offered.
        Assert.Equal(["noname", "Solo", "Abe Adams", "Ann Adams", "Zed Zimmer"], providers!.Select(p => p.DisplayName));
        Assert.Equal([25L, 27L, 22L, 21L, 20L], providers!.Select(p => p.Id));
    }

    [Fact]
    public async Task A_provider_from_the_lookup_is_accepted_as_an_encounters_provider()
    {
        var providers = await _client.GetFromJsonAsync<List<ProviderLookupResponse>>("/api/v1/lookups/providers");
        var chosen = providers!.First(p => p.DisplayName == "Zed Zimmer");

        var response = await _client.PostAsJsonAsync(
            "/api/v1/encounters",
            new CreateEncounterRequest(EncounterTestData.PatientUuid, "AMB", 5, null, "visit", 3, null, chosen.Id, null, null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<EncounterCreatedResponse>();
        var stored = await _client.GetFromJsonAsync<EncounterResponse>($"/api/v1/encounters/{created!.Uuid}");
        Assert.Equal(chosen.Id, stored!.ProviderId);   // not the signed-in user (7): the one the caller chose from the list
    }

    [Fact]
    public async Task Categories_are_the_active_patient_visit_ones_in_legacys_sequence_without_no_show_events_or_therapy_groups()
    {
        var categories = await _client.GetFromJsonAsync<List<CalendarCategoryLookupResponse>>("/api/v1/lookups/calendar-categories");

        // pc_seq order, then id: Established Patient (10, id 9), Office Visit (20, id 5), No Constant (30, id 14). Not offered: No Show, the provider and clinic events,
        // group therapy (type 3) and the inactive one.
        Assert.Equal(["Established Patient", "Office Visit", "No Constant"], categories!.Select(c => c.Name));
        Assert.Equal([9, 5, 14], categories!.Select(c => c.Id));
    }

    [Fact]
    public async Task With_nothing_configured_every_list_is_empty_not_an_error()
    {
        await fixture.RootExecuteAsync("DELETE FROM openemr_postcalendar_categories");
        await fixture.RootExecuteAsync("DELETE FROM facility");
        await fixture.RootExecuteAsync("DELETE FROM list_options");

        Assert.Empty((await _client.GetFromJsonAsync<List<CalendarCategoryLookupResponse>>("/api/v1/lookups/calendar-categories"))!);
        Assert.Empty((await _client.GetFromJsonAsync<List<FacilityLookupResponse>>("/api/v1/lookups/facilities"))!);
        Assert.Empty((await _client.GetFromJsonAsync<List<EncounterClassLookupResponse>>("/api/v1/lookups/encounter-classes"))!);
    }

    [Fact]
    public async Task A_name_legacy_left_empty_reads_as_an_empty_name_not_a_failure()
    {
        await fixture.RootExecuteAsync("UPDATE facility SET name = NULL WHERE id = 4");
        await fixture.RootExecuteAsync("UPDATE openemr_postcalendar_categories SET pc_catname = NULL WHERE pc_catid = 14");

        var facilities = await _client.GetFromJsonAsync<List<FacilityLookupResponse>>("/api/v1/lookups/facilities");
        var categories = await _client.GetFromJsonAsync<List<CalendarCategoryLookupResponse>>("/api/v1/lookups/calendar-categories");

        Assert.Contains(facilities!, f => f.Id == 4 && f.Name == "");
        Assert.Contains(categories!, c => c.Id == 14 && c.Name == "");
    }

    [Fact]
    public async Task A_lookup_is_bounded_so_a_misconfigured_install_cannot_make_it_unbounded()
    {
        await fixture.RootExecuteAsync(
            $"INSERT INTO facility (id, name, inactive) SELECT seq + 1000, CONCAT('Generated ', seq), 0 FROM seq_1_to_{EfEncounterLookups.MaxRows + 50}");

        var facilities = await _client.GetFromJsonAsync<List<FacilityLookupResponse>>("/api/v1/lookups/facilities");

        Assert.Equal(EfEncounterLookups.MaxRows, facilities!.Count);
    }

    [Fact]
    public async Task What_the_lookups_offer_is_what_creating_an_encounter_accepts()
    {
        var @class = (await _client.GetFromJsonAsync<List<EncounterClassLookupResponse>>("/api/v1/lookups/encounter-classes"))![1];
        var facility = (await _client.GetFromJsonAsync<List<FacilityLookupResponse>>("/api/v1/lookups/facilities"))!.First(f => f.Id == 3);
        var category = (await _client.GetFromJsonAsync<List<CalendarCategoryLookupResponse>>("/api/v1/lookups/calendar-categories"))![0];

        var response = await _client.PostAsJsonAsync(
            "/api/v1/encounters", new CreateEncounterRequest(EncounterTestData.PatientUuid, @class.Code, category.Id, null, "visit", facility.Id, null, 7, null, null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Every_lookup_is_401_without_the_gateway_secret()
    {
        using var anonymous = _factory.CreateClient();

        foreach (var path in new[] { "encounter-classes", "facilities", "calendar-categories", "providers", "immunization-options" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/v1/lookups/{path}")).StatusCode);
    }

    [Fact]
    public async Task The_providers_lookup_offers_a_name_and_an_id_and_nothing_else_about_the_user()
    {
        var json = await _client.GetStringAsync("/api/v1/lookups/providers");

        using var doc = System.Text.Json.JsonDocument.Parse(json);
        foreach (var provider in doc.RootElement.EnumerateArray())
            Assert.Equal(["displayName", "id"], provider.EnumerateObject().Select(p => p.Name).Order());
    }

    [Fact]
    public async Task The_immunization_options_come_back_in_one_call_and_what_they_offer_is_accepted_when_recording()
    {
        await ImmunizationTestData.SeedCodesAsync(fixture);

        var options = await _client.GetFromJsonAsync<ImmunizationOptionsResponse>("/api/v1/lookups/immunization-options");

        Assert.NotNull(options);
        Assert.Equal(["6", "7", "27"], options.Vaccines.Select(o => o.Id));
        var response = await _client.PostAsJsonAsync(
            "/api/v1/immunizations",
            new RecordImmunizationRequest(
                EncounterTestData.PatientUuid, new DateTime(2026, 11, 10, 8, 0, 0), int.Parse(options.Vaccines[0].Id), null, null, null, null, 0.5m, options.Units[0].Id, options.Routes[0].Id,
                options.Sites[0].Id, options.CompletionStatuses[0].Id, options.InformationSources[0].Id, null, null, null, null, null, null, null, null, null, null, false, 7));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
