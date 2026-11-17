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
}
