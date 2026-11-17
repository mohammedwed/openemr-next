using OpenEmrNext.ClinicalCare.Infrastructure;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>The immunization code lists read from legacy's <c>list_options</c> by the production principal: the right lists, exact ids, nothing from another list.</summary>
[Collection("ClinicalCareContainer")]
public class ImmunizationCodeListsTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Every_list_is_read_with_its_exact_option_ids_and_nothing_leaks_in_from_another_list()
    {
        await ImmunizationTestData.SeedCodesAsync(fixture);
        await using var db = fixture.CreateDbContext();

        var codes = await new EfImmunizationCodeLists(db).GetCodesAsync(CancellationToken.None);

        Assert.Equal([6, 7, 27], codes.Vaccines.Order());   // 'not-a-number' cannot be stored in an int column, so it is left out
        Assert.Equal(["bymouth", "intramuscular"], codes.Routes.Order());
        Assert.Equal(["left_arm", "right_arm"], codes.Sites.Order());
        Assert.Equal(["Completed", "Refused"], codes.CompletionStatuses.Order());
        Assert.Equal(["00", "01"], codes.InformationSources.Order());
        Assert.Equal(["OSTUDY", "PATOBJ"], codes.RefusalReasons.Order());
        Assert.Equal(["1", "9"], codes.Units.Order());
    }

    [Fact]
    public async Task With_no_lists_configured_every_set_is_empty_not_an_error()
    {
        await using var db = fixture.CreateDbContext();

        var codes = await new EfImmunizationCodeLists(db).GetCodesAsync(CancellationToken.None);

        Assert.Empty(codes.Vaccines);
        Assert.Empty(codes.Routes);
        Assert.Empty(codes.CompletionStatuses);
    }

    [Fact]
    public async Task The_options_offered_are_the_active_ones_with_their_labels_in_legacys_order_and_only_whole_number_vaccines()
    {
        await ImmunizationTestData.SeedCodesAsync(fixture);
        await fixture.RootExecuteAsync("INSERT INTO list_options (list_id, option_id, title, seq, is_default, activity) VALUES ('drug_route', 'retired', 'Retired route', 0, 0, 0), ('drug_route', '', 'None', 0, 0, 1)");
        await using var db = fixture.CreateDbContext();

        var options = await new EfImmunizationLookups(db).GetOptionsAsync(CancellationToken.None);

        Assert.Equal(["6", "7", "27"], options.Vaccines.Select(o => o.Id));                    // 'not-a-number' cannot be stored in an int column
        Assert.Equal(["DT 1", "DT 2", "Flu"], options.Vaccines.Select(o => o.Title));
        Assert.Equal(["intramuscular", "bymouth"], options.Routes.Select(o => o.Id));         // the retired and the blank one are not offered
        Assert.Equal(["left_arm", "right_arm"], options.Sites.Select(o => o.Id));
        Assert.Equal(["Completed", "Refused"], options.CompletionStatuses.Select(o => o.Id));
        Assert.Equal(["00", "01"], options.InformationSources.Select(o => o.Id));
        Assert.Equal(["OSTUDY", "PATOBJ"], options.RefusalReasons.Select(o => o.Id));
        Assert.Equal(["1", "9"], options.Units.Select(o => o.Id));
    }

    [Fact]
    public async Task With_no_lists_configured_every_option_list_is_empty_and_an_untitled_option_is_labelled_by_its_id()
    {
        await using var db = fixture.CreateDbContext();
        Assert.Empty((await new EfImmunizationLookups(db).GetOptionsAsync(CancellationToken.None)).Vaccines);

        await fixture.RootExecuteAsync("INSERT INTO list_options (list_id, option_id, title, seq, is_default, activity) VALUES ('drug_route', 'x', '', 1, 0, 1)");
        await using var again = fixture.CreateDbContext();
        Assert.Equal("x", Assert.Single((await new EfImmunizationLookups(again).GetOptionsAsync(CancellationToken.None)).Routes).Title);
    }
}
