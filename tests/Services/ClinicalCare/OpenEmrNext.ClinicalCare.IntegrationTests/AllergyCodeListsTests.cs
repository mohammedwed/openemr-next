using OpenEmrNext.ClinicalCare.Infrastructure;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>The three allergy code lists, read from <c>list_options</c> as the production principal.</summary>
[Collection("ClinicalCareContainer")]
public class AllergyCodeListsTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private Task AddAsync(string list, string option) =>
        fixture.RootExecuteAsync($"INSERT INTO list_options (list_id, option_id, title, seq, is_default) VALUES ('{list}', '{option}', 't', 1, 0)");

    private async Task<OpenEmrNext.ClinicalCare.Domain.ValueObjects.AllergyCodeData> ReadAsync()
    {
        await using var db = fixture.CreateDbContext();
        return await new EfAllergyCodeLists(db).GetCodesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Each_list_is_read_as_the_exact_string_codes_legacy_stores()
    {
        await AddAsync("reaction", "hives");
        await AddAsync("reaction", "nausea");
        await AddAsync("severity_ccda", "mild_to_moderate");
        await AddAsync("allergyintolerance-verification", "entered-in-error");

        var codes = await ReadAsync();

        Assert.Equal(["hives", "nausea"], codes.Reactions.Order());
        Assert.Equal(["mild_to_moderate"], codes.Severities);
        Assert.Equal(["entered-in-error"], codes.Verifications);
    }

    [Fact]
    public async Task A_problems_verification_list_and_other_lists_never_leak_into_the_allergy_ones()
    {
        await AddAsync("condition-verification", "differential");
        await AddAsync("allergyintolerance-verification", "confirmed");
        await AddAsync("outcome", "1");

        var codes = await ReadAsync();

        Assert.Equal(["confirmed"], codes.Verifications);
        Assert.Empty(codes.Reactions);
        Assert.Empty(codes.Severities);
    }

    [Fact]
    public async Task Option_ids_are_not_case_folded_so_the_domains_exact_match_is_meaningful()
    {
        await AddAsync("reaction", "Hives");

        var codes = await ReadAsync();

        Assert.Contains("Hives", codes.Reactions);
        Assert.DoesNotContain("hives", codes.Reactions);
    }

    [Fact]
    public async Task No_configured_lists_is_empty_sets_not_an_error()
    {
        var codes = await ReadAsync();

        Assert.Empty(codes.Reactions);
        Assert.Empty(codes.Severities);
        Assert.Empty(codes.Verifications);
    }
}
