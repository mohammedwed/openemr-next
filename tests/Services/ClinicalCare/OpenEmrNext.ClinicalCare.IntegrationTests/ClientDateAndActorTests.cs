using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenEmrNext.ClinicalCare.Api.Contracts.Responses;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// R-11 (a date's zone is honoured, not dropped) and R-12 (the provider's group comes from <c>groups</c>, with one fallback), through the real
/// API and a real database.
/// </summary>
[Collection("ClinicalCareContainer")]
public class ClientDateAndActorTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
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

    private async Task<HttpResponseMessage> PostAsync(string extra, long provider = 7) =>
        await _client.PostAsync("/api/v1/encounters", new StringContent(
            $$"""{ "patientUuid": "{{EncounterTestData.PatientUuid}}", "calendarCategoryId": 5, "providerId": {{provider}} {{extra}} }""",
            Encoding.UTF8, "application/json"));

    private Task<string?> StoredDateAsync() =>
        fixture.RootScalarAsync<string>("SELECT DATE_FORMAT(date, '%Y-%m-%d %H:%i:%s') FROM form_encounter");

    private static string Local(string iso) => DateTimeOffset.Parse(iso).LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");

    [Theory]
    [InlineData("2026-10-01T08:30:00Z")]
    [InlineData("2026-10-01T08:30:00+02:00")]
    [InlineData("2026-10-01T08:30:00-05:00")]
    public async Task R_11_a_date_with_a_zone_is_an_instant_and_is_stored_as_the_servers_local_time_not_its_clock_digits(string sent)
    {
        var response = await PostAsync($$""", "date": "{{sent}}" """);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        // The expected value is computed by the platform from the same instant, independently of the code under test, so the test
        // holds whatever time zone the machine running it is in.
        Assert.Equal(Local(sent), await StoredDateAsync());
    }

    [Fact]
    public async Task R_11_a_date_without_a_zone_is_taken_to_be_local_already_and_stored_exactly_as_given()
    {
        var response = await PostAsync(""", "date": "2026-10-01T08:30:00" """);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("2026-10-01 08:30:00", await StoredDateAsync());
    }

    [Fact]
    public async Task R_11_the_onset_date_follows_the_same_rule()
    {
        await PostAsync(""", "onsetDate": "2026-09-30T22:00:00Z" """);

        Assert.Equal(Local("2026-09-30T22:00:00Z"),
            await fixture.RootScalarAsync<string>("SELECT DATE_FORMAT(onset_date, '%Y-%m-%d %H:%i:%s') FROM form_encounter"));
    }

    [Fact]
    public async Task R_12_a_provider_in_a_non_default_group_is_recorded_with_that_group_on_the_forms_row()
    {
        await fixture.RootExecuteAsync("INSERT INTO users (id, username) VALUES (8, 'nurse_joy')");
        await fixture.RootExecuteAsync("INSERT INTO `groups` (name, `user`) VALUES ('Clinic Staff', 'nurse_joy')");

        var created = await (await PostAsync("", provider: 8)).Content.ReadFromJsonAsync<EncounterCreatedResponse>();

        Assert.NotNull(created);
        Assert.Equal("nurse_joy", await fixture.RootScalarAsync<string>("SELECT user FROM forms"));
        Assert.Equal("Clinic Staff", await fixture.RootScalarAsync<string>("SELECT groupname FROM forms"));
    }

    [Fact]
    public async Task R_12_a_provider_with_no_group_row_gets_the_single_default_and_an_unknown_provider_gets_a_null_user()
    {
        await fixture.RootExecuteAsync("INSERT INTO users (id, username) VALUES (9, 'no_group_user')");

        await PostAsync("", provider: 9);
        await PostAsync("", provider: 999);

        Assert.Equal(2L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM forms WHERE groupname = 'Default'"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM forms WHERE user = 'no_group_user'"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM forms WHERE user IS NULL"));
    }
}
