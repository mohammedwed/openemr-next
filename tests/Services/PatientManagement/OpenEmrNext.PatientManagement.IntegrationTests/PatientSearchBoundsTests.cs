using System.Net;
using System.Net.Http.Json;
using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using OpenEmrNext.PatientManagement.Api.Contracts.Requests;
using OpenEmrNext.PatientManagement.Api.Contracts.Responses;
using OpenEmrNext.PatientManagement.Api.Endpoints;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace OpenEmrNext.PatientManagement.IntegrationTests;

/// <summary>
/// The patient search is a bounded page, and what a caller types is only ever data. Each test registers patients under a token
/// of its own, so it counts only its own rows however many other patients the shared database holds, and each is written so it
/// fails if the behaviour it names is taken out: the paging, the range checks, the wildcard escaping, and the parameter binding.
/// </summary>
[Collection("PatientDataContainer")]
public class PatientSearchBoundsTests(PatientDataContainerFixture fixture) : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private readonly DirectoryInfo _repositoryRoot = Directory.CreateTempSubdirectory("patient-search-bounds-tests-");

    public Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            [
                new("PatientManagementDb:Host", fixture.Hostname),
                new("PatientManagementDb:Port", fixture.Port.ToString()),
                new("PatientManagementDb:Database", fixture.Database),
                new("PatientManagementDb:User", fixture.User),
                new("PatientManagementDb:Password", fixture.DbPassword),
                new("AdvanceDirectiveStorage:RepositoryRoot", _repositoryRoot.FullName),
                new("InternalGateway:Secret", "test-secret")
            ])));
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Internal-Gateway-Secret", "test-secret");
        _client.DefaultRequestHeaders.Add("X-Authenticated-User", PatientDataContainerFixture.TestUserUuid.ToString());
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        _repositoryRoot.Delete(recursive: true);
        return Task.CompletedTask;
    }

    private static string Token() => Guid.NewGuid().ToString("N");

    private async Task<Guid> RegisterAsync(string lastName, string firstName = "Bounds")
    {
        var response = await _client!.PostAsJsonAsync("/api/v1/patients", new RegisterPatientRequest(
            firstName, lastName, "female", new DateOnly(1993, 7, 7), null, null, 1));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PatientRegisteredResponse>())!.Uuid;
    }

    private async Task<(HttpStatusCode Status, Bundle? Bundle, OperationOutcome? Outcome)> SearchAsync(string pathAndQuery)
    {
        var response = await _client!.GetAsync(pathAndQuery);
        var body = await response.Content.ReadAsStringAsync();
        return response.StatusCode == HttpStatusCode.OK
            ? (response.StatusCode, FhirJson.Parse<Bundle>(body), null)
            : (response.StatusCode, null, FhirJson.Parse<OperationOutcome>(body));
    }

    private static string NextPath(Bundle bundle)
    {
        var url = bundle.Link.Single(l => l.Relation == "next").Url;
        return url[url.IndexOf("/fhir", StringComparison.Ordinal)..];
    }

    [Fact]
    public async Task A_search_is_a_page_and_the_next_links_walk_every_match_exactly_once()
    {
        var token = Token();
        var registered = new List<Guid>();
        for (var i = 0; i < 5; i++)
            registered.Add(await RegisterAsync($"Page{token}{i}"));

        var seen = new List<string>();
        var (status, page, _) = await SearchAsync($"/fhir/Patient?family={token}&_count=2");
        Assert.Equal(HttpStatusCode.OK, status);
        var pages = 0;
        while (true)
        {
            pages++;
            Assert.Equal(5, page!.Total);                       // the total is every match, not the size of this page
            Assert.True(page.Entry.Count <= 2);
            seen.AddRange(page.Entry.Select(e => e.Resource.Id));
            if (page.Link.All(l => l.Relation != "next"))
                break;
            page = (await SearchAsync(NextPath(page))).Bundle;
        }

        Assert.Equal(3, pages);                                 // 2 + 2 + 1
        Assert.Equal(5, seen.Count);
        Assert.Equal(5, seen.Distinct().Count());               // a page never repeats a patient
        Assert.Equal(registered.Select(r => r.ToString()).Order(), seen.Order());

        // The order is total and stable: the same walk twice, and the whole set in one page, all give the same sequence.
        // (Ids in this database are not handed out in registration order, so the test compares sequences, not registration order.)
        var allAtOnce = (await SearchAsync($"/fhir/Patient?family={token}&_count=100")).Bundle!.Entry.Select(e => e.Resource.Id).ToList();
        Assert.Equal(allAtOnce, seen);
    }

    [Fact]
    public async Task A_search_with_no_count_returns_the_default_page_and_says_so_in_its_self_link()
    {
        var (status, bundle, _) = await SearchAsync("/fhir/Patient");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(bundle!.Entry.Count <= 25);
        Assert.Contains("_count=25", bundle.Link.Single(l => l.Relation == "self").Url);
    }

    [Theory]
    [InlineData("_count=0")]
    [InlineData("_count=101")]
    [InlineData("_count=-5")]
    [InlineData("_count=lots")]
    [InlineData("_offset=-1")]
    [InlineData("_offset=first")]
    public async Task A_count_or_offset_out_of_range_is_refused_with_an_operation_outcome(string query)
    {
        var (status, _, outcome) = await SearchAsync($"/fhir/Patient?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains(outcome!.Issue, i => (i.Diagnostics ?? "").Contains("'_count'") || (i.Diagnostics ?? "").Contains("'_offset'"));
    }

    [Fact]
    public async Task The_largest_allowed_page_is_accepted()
    {
        var (status, _, _) = await SearchAsync("/fhir/Patient?_count=100");

        Assert.Equal(HttpStatusCode.OK, status);
    }

    [Fact]
    public async Task Percent_and_underscore_in_a_search_match_only_themselves_not_any_text()
    {
        var token = Token();
        await RegisterAsync($"Wild{token}");

        // Each of these would match the patient if '%' or '_' acted as a wildcard.
        foreach (var query in new[] { $"family=W_ld{token}", $"family=%25{token}", $"family=Wild{token[..^1]}_", $"name=W%25ld{token}", $"address-city=%25" })
        {
            var (status, bundle, _) = await SearchAsync($"/fhir/Patient?{query}");
            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Equal(0, bundle!.Total);
        }

        // And the plain text still finds it.
        Assert.Equal(1, (await SearchAsync($"/fhir/Patient?family=Wild{token}")).Bundle!.Total);
    }

    [Fact]
    public async Task A_literal_percent_is_matched_as_a_literal_percent()
    {
        var token = Token();
        var uuid = await RegisterAsync($"Pct{token}%");

        var (_, bundle, _) = await SearchAsync($"/fhir/Patient?family={token}%25");

        Assert.Equal(uuid.ToString(), Assert.Single(bundle!.Entry).Resource.Id);
    }

    [Theory]
    [InlineData("family", "x' OR '1'='1")]
    [InlineData("family", "'; DROP TABLE patient_data; --")]
    [InlineData("given", "\" OR 1=1 --")]
    [InlineData("name", "') UNION SELECT 1,2,3 -- ")]
    [InlineData("identifier", "1' OR '1'='1")]
    [InlineData("address-city", "x'; DELETE FROM patient_data; --")]
    [InlineData("email", "a@b.c' OR 1=1 --")]
    [InlineData("gender", "female' OR '1'='1")]
    [InlineData("telecom", "0'; SELECT SLEEP(5); --")]
    public async Task Hostile_text_in_a_search_matches_nothing_and_changes_nothing(string parameter, string payload)
    {
        var token = Token();
        var uuid = await RegisterAsync($"Safe{token}");

        var (status, bundle, _) = await SearchAsync($"/fhir/Patient?{parameter}={Uri.EscapeDataString(payload)}");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(0, bundle!.Total);                         // not "everyone": the text is a value, not part of the statement
        // The table is intact and the patient is still there to be found.
        var (_, after, _) = await SearchAsync($"/fhir/Patient?family=Safe{token}");
        Assert.Equal(uuid.ToString(), Assert.Single(after!.Entry).Resource.Id);
    }

    [Fact]
    public async Task A_name_with_quotes_is_stored_and_read_back_exactly()
    {
        var token = Token();
        var lastName = $"O'Neil{token}";
        var uuid = await RegisterAsync(lastName, "Ann-\"Marie\"");

        var response = await _client!.GetAsync($"/fhir/Patient/{uuid}");

        var patient = FhirJson.Parse<Patient>(await response.Content.ReadAsStringAsync());
        Assert.Equal(lastName, patient.Name[0].Family);
        Assert.Equal("Ann-\"Marie\"", patient.Name[0].Given.First());
        Assert.Equal(1, (await SearchAsync($"/fhir/Patient?family={Uri.EscapeDataString("O'Neil" + token)}")).Bundle!.Total);
    }

    [Fact]
    public async Task The_plain_list_is_bounded_and_reports_how_many_matched()
    {
        var token = Token();
        for (var i = 0; i < 3; i++)
            await RegisterAsync($"List{token}{i}");

        var response = await _client!.GetAsync($"/api/v1/patients?name={token}&limit=2&offset=0");
        var second = await _client.GetAsync($"/api/v1/patients?name={token}&limit=2&offset=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, (await response.Content.ReadFromJsonAsync<List<PatientSummaryResponse>>())!.Count);
        Assert.Equal("3", response.Headers.GetValues("X-Total-Count").Single());
        Assert.Single((await second.Content.ReadFromJsonAsync<List<PatientSummaryResponse>>())!);
    }

    [Theory]
    [InlineData("limit=0")]
    [InlineData("limit=101")]
    [InlineData("offset=-1")]
    public async Task The_plain_list_refuses_a_limit_or_offset_out_of_range(string query)
    {
        var response = await _client!.GetAsync($"/api/v1/patients?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_plain_list_without_a_limit_is_capped_at_the_maximum()
    {
        var response = await _client!.GetAsync("/api/v1/patients");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<List<PatientSummaryResponse>>())!.Count <= 100);
        Assert.True(response.Headers.Contains("X-Total-Count"));
    }
}
