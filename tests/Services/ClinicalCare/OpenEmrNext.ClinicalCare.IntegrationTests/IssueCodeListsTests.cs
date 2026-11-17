using OpenEmrNext.ClinicalCare.Infrastructure;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>The code lists an issue is validated against, read from <c>list_options</c> as the production principal.</summary>
[Collection("ClinicalCareContainer")]
public class IssueCodeListsTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private Task AddAsync(string list, string option) =>
        fixture.RootExecuteAsync($"INSERT INTO list_options (list_id, option_id, title, seq, is_default) VALUES ('{list}', '{option}', 't', 1, 0)");

    private async Task<OpenEmrNext.ClinicalCare.Domain.ValueObjects.IssueCodeData> ReadAsync()
    {
        await using var db = fixture.CreateDbContext();
        return await new EfIssueCodeLists(db).GetCodesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Each_list_is_read_as_the_numeric_codes_legacy_stores()
    {
        foreach (var o in new[] { "0", "1", "2" }) await AddAsync("outcome", o);
        foreach (var o in new[] { "0", "4", "8" }) await AddAsync("occurrence", o);

        var codes = await ReadAsync();

        Assert.Equal([0, 1, 2], codes.Outcomes.Order());
        Assert.Equal([0, 4, 8], codes.Occurrences.Order());
    }

    [Fact]
    public async Task Another_list_with_the_same_option_ids_never_leaks_in()
    {
        await AddAsync("outcome", "1");
        await AddAsync("severity_ccda", "7");
        await AddAsync("other_list", "9");

        var codes = await ReadAsync();

        Assert.Equal([1], codes.Outcomes);
        Assert.Empty(codes.Occurrences);
    }

    [Fact]
    public async Task An_option_that_is_not_a_whole_number_cannot_be_stored_in_the_int_column_and_is_left_out()
    {
        await AddAsync("outcome", "1");
        await AddAsync("outcome", "unassigned");
        await AddAsync("outcome", "2.5");
        await AddAsync("outcome", "");

        var codes = await ReadAsync();

        Assert.Equal([1], codes.Outcomes);
    }

    [Fact]
    public async Task No_configured_lists_is_empty_sets_not_an_error()
    {
        var codes = await ReadAsync();

        Assert.Empty(codes.Outcomes);
        Assert.Empty(codes.Occurrences);
    }

    [Fact]
    public async Task The_condition_verification_list_is_read_as_exact_strings_and_never_mixes_with_the_allergy_one()
    {
        await AddAsync("condition-verification", "unconfirmed");
        await AddAsync("condition-verification", "Confirmed");
        await AddAsync("allergyintolerance-verification", "differential");

        var codes = await ReadAsync();

        Assert.Equal(["Confirmed", "unconfirmed"], codes.Verifications.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task With_no_condition_verification_list_configured_the_set_is_empty()
    {
        var codes = await ReadAsync();

        Assert.Empty(codes.Verifications);
    }
}
