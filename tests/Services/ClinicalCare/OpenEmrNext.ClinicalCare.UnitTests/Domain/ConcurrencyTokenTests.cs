using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.UnitTests.Domain;

/// <summary>
/// The concurrency token (LEG-10, ADR 0068): a row stamp as a strong ETag, and what an <c>If-Match</c> value may and may not match.
/// What is rejected matters as much as what is accepted: a parse that is too generous lets a stale write through.
/// </summary>
public class ConcurrencyTokenTests
{
    private static readonly DateTime Stamp = new(2026, 11, 17, 9, 30, 15, DateTimeKind.Unspecified);

    [Fact]
    public void An_etag_is_a_quoted_strong_tag()
    {
        var etag = ConcurrencyToken.From(Stamp).ToETag();

        Assert.StartsWith("\"", etag);
        Assert.EndsWith("\"", etag);
        Assert.DoesNotContain("W/", etag);
    }

    [Fact]
    public void An_etag_round_trips_through_if_match()
    {
        var token = ConcurrencyToken.From(Stamp);

        var parsed = ConcurrencyToken.ParseIfMatch(token.ToETag());

        Assert.Equal(token, Assert.Single(parsed));
    }

    [Fact]
    public void Sub_second_parts_are_dropped_because_the_column_cannot_hold_them()
    {
        var withFraction = Stamp.AddTicks(1234567);

        Assert.Equal(ConcurrencyToken.From(Stamp), ConcurrencyToken.From(withFraction));
        Assert.Equal(ConcurrencyToken.From(Stamp).ToETag(), ConcurrencyToken.From(withFraction).ToETag());
    }

    [Fact]
    public void Different_stamps_give_different_tags()
    {
        Assert.NotEqual(ConcurrencyToken.From(Stamp).ToETag(), ConcurrencyToken.From(Stamp.AddSeconds(1)).ToETag());
    }

    [Fact]
    public void The_stamps_kind_does_not_change_the_tag()
    {
        var local = DateTime.SpecifyKind(Stamp, DateTimeKind.Local);

        Assert.Equal(ConcurrencyToken.From(Stamp).ToETag(), ConcurrencyToken.From(local).ToETag());
    }

    [Fact]
    public void A_list_is_allowed_and_each_readable_tag_comes_back()
    {
        var a = ConcurrencyToken.From(Stamp);
        var b = ConcurrencyToken.From(Stamp.AddSeconds(5));

        var parsed = ConcurrencyToken.ParseIfMatch($"{a.ToETag()} , {b.ToETag()}");

        Assert.Equal([a, b], parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_missing_header_matches_nothing(string? header) =>
        Assert.Empty(ConcurrencyToken.ParseIfMatch(header));

    [Fact]
    public void A_weak_tag_never_matches_because_if_match_compares_strongly()
    {
        var weak = "W/" + ConcurrencyToken.From(Stamp).ToETag();

        Assert.Empty(ConcurrencyToken.ParseIfMatch(weak));
    }

    [Theory]
    [InlineData("*")]
    [InlineData("garbage")]
    [InlineData("\"\"")]
    [InlineData("\"zz\"")]
    [InlineData("\"-1\"")]
    [InlineData("\"0\"")]
    [InlineData("\"ffffffffffffffffffff\"")]
    [InlineData("\"1\"")]               // a real tick count, but not a whole second: no stamp we issued
    [InlineData("unquoted")]
    public void Anything_that_is_not_a_tag_we_issued_matches_nothing(string header) =>
        Assert.Empty(ConcurrencyToken.ParseIfMatch(header));

    [Fact]
    public void An_unreadable_entry_in_a_list_does_not_hide_a_readable_one()
    {
        var token = ConcurrencyToken.From(Stamp);

        var parsed = ConcurrencyToken.ParseIfMatch($"garbage, {token.ToETag()}");

        Assert.Equal(token, Assert.Single(parsed));
    }
}
