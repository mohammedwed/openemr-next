using OpenEmrNext.ClinicalCare.Api.Fhir;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// The FHIR date search value (ADR 0079 #6): what range a value means, what each prefix does with it, how a zone is read, and what is refused. Pure: no database. The stamps being searched are the server's local
/// wall-clock times, so expectations are written in local time.
/// </summary>
public class FhirDateSearchTests
{
    private static UpdatedWindow Parse(string value)
    {
        Assert.True(FhirDateSearch.TryParse(value, out var window, out var problem), problem);
        return window;
    }

    [Theory]
    [InlineData("2026", "2026-01-01T00:00:00", "2027-01-01T00:00:00")]
    [InlineData("2026-03", "2026-03-01T00:00:00", "2026-04-01T00:00:00")]
    [InlineData("2026-12", "2026-12-01T00:00:00", "2027-01-01T00:00:00")]
    [InlineData("2026-03-15", "2026-03-15T00:00:00", "2026-03-16T00:00:00")]
    [InlineData("2026-02-28", "2026-02-28T00:00:00", "2026-03-01T00:00:00")]
    [InlineData("2026-03-15T10:30", "2026-03-15T10:30:00", "2026-03-15T10:31:00")]
    [InlineData("2026-03-15T10:30:15", "2026-03-15T10:30:15", "2026-03-15T10:30:16")]
    [InlineData("2026-03-15T10:30:15.250", "2026-03-15T10:30:15", "2026-03-15T10:30:16")]
    public void A_value_means_the_range_its_precision_gives_and_a_bare_value_is_equals(string value, string from, string until)
    {
        var window = Parse(value);

        Assert.Equal(DateTime.Parse(from), window.From);
        Assert.Equal(DateTime.Parse(until), window.Until);
        Assert.Equal(window, Parse("eq" + value));
    }

    [Theory]
    [InlineData("ge2026-03-15", "2026-03-15T00:00:00", null)]    // from the start of the range
    [InlineData("gt2026-03-15", "2026-03-16T00:00:00", null)]    // after the end of the range: the day itself is not after itself
    [InlineData("lt2026-03-15", null, "2026-03-15T00:00:00")]    // before the start
    [InlineData("le2026-03-15", null, "2026-03-16T00:00:00")]    // up to the end of the range: the day itself is included
    [InlineData("ge2026", "2026-01-01T00:00:00", null)]
    [InlineData("gt2026", "2027-01-01T00:00:00", null)]
    [InlineData("le2026-03", null, "2026-04-01T00:00:00")]
    public void Each_prefix_takes_the_ranges_start_or_end(string value, string? from, string? until)
    {
        var window = Parse(value);

        Assert.Equal(from is null ? null : DateTime.Parse(from), window.From);
        Assert.Equal(until is null ? null : DateTime.Parse(until), window.Until);
    }

    [Theory]
    [InlineData("2026-03-15T10:00:00Z")]
    [InlineData("2026-03-15T10:00:00+05:30")]
    [InlineData("2026-03-15T10:00:00-08:00")]
    public void A_value_with_a_zone_is_an_instant_converted_to_the_servers_local_time(string value)
    {
        var expected = DateTimeOffset.Parse(value, System.Globalization.CultureInfo.InvariantCulture).ToLocalTime().DateTime;

        Assert.Equal(expected, Parse(value).From);
    }

    [Fact]
    public void A_plus_that_arrived_as_a_space_is_read_as_the_plus_it_was()
    {
        // A "+" in a query string reaches the service as a space.
        Assert.Equal(Parse("2026-03-15T10:00:00+02:00"), Parse("2026-03-15T10:00:00 02:00"));
    }

    [Fact]
    public void A_value_without_a_zone_is_taken_as_local_time()
    {
        Assert.Equal(new DateTime(2026, 3, 15, 10, 0, 0), Parse("2026-03-15T10:00:00").From);
    }

    [Theory]
    [InlineData("ne2026")]
    [InlineData("sa2026")]
    [InlineData("eb2026")]
    [InlineData("ap2026")]
    [InlineData("2026-13")]
    [InlineData("2026-02-30")]
    [InlineData("2026-03-15T25:00")]
    [InlineData("2026-03-15T10:61")]
    [InlineData("2026-3-1")]
    [InlineData("26")]
    [InlineData("yesterday")]
    [InlineData("")]
    [InlineData("ge")]
    [InlineData("2026-03-15 10:00")]
    [InlineData("2026-03-15T10:00:00Zextra")]
    public void A_value_that_is_not_a_date_or_whose_prefix_is_not_supported_is_refused_with_a_reason(string value)
    {
        Assert.False(FhirDateSearch.TryParse(value, out _, out var problem));
        Assert.False(string.IsNullOrWhiteSpace(problem));
    }

    [Fact]
    public void The_refusal_of_a_prefix_says_which_are_supported()
    {
        FhirDateSearch.TryParse("ne2026", out _, out var problem);

        Assert.Contains("'ne'", problem);
        Assert.Contains("eq, gt, ge, lt or le", problem);
    }

    // The window itself

    [Fact]
    public void A_stamp_is_inside_when_it_is_at_or_after_the_start_and_before_the_end()
    {
        var window = new UpdatedWindow(new DateTime(2026, 3, 1), new DateTime(2026, 4, 1));

        Assert.True(window.Contains(new DateTime(2026, 3, 1)));
        Assert.True(window.Contains(new DateTime(2026, 3, 31, 23, 59, 59)));
        Assert.False(window.Contains(new DateTime(2026, 4, 1)));
        Assert.False(window.Contains(new DateTime(2026, 2, 28, 23, 59, 59)));
    }

    [Fact]
    public void An_open_end_is_open_and_a_record_with_no_stamp_is_never_inside()
    {
        Assert.True(new UpdatedWindow(new DateTime(2026, 3, 1), null).Contains(new DateTime(2099, 1, 1)));
        Assert.True(new UpdatedWindow(null, new DateTime(2026, 3, 1)).Contains(new DateTime(1990, 1, 1)));
        Assert.False(new UpdatedWindow(null, null).Contains(null));
    }

    [Fact]
    public void Two_windows_together_are_the_overlap_and_a_range_that_cannot_hold_anything_holds_nothing()
    {
        var range = Parse("ge2026-02-01").Intersect(Parse("lt2026-03-01"));
        var contradiction = Parse("ge2026-03-01").Intersect(Parse("lt2026-01-01"));

        Assert.True(range.Contains(new DateTime(2026, 2, 15)));
        Assert.False(range.Contains(new DateTime(2026, 3, 1)));
        Assert.False(range.Contains(new DateTime(2026, 1, 31)));
        Assert.False(contradiction.Contains(new DateTime(2026, 2, 1)));
        Assert.False(contradiction.Contains(new DateTime(2025, 1, 1)));
    }
}
