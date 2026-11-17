using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.UnitTests.Immunizations;

/// <summary>The content version (ADR 0072): a hash of canonical text with an <c>ETag</c> of its own shape, so it detects any change and can never be confused with a stamp's tag.</summary>
public class ContentVersionTests
{
    [Fact]
    public void The_same_parts_always_give_the_same_version() =>
        Assert.Equal(ContentVersion.Of(["a", "b", null]), ContentVersion.Of(["a", "b", null]));

    [Fact]
    public void Any_change_to_any_part_changes_the_version()
    {
        var baseline = ContentVersion.Of(["a", "b", "c"]);

        Assert.NotEqual(baseline, ContentVersion.Of(["x", "b", "c"]));
        Assert.NotEqual(baseline, ContentVersion.Of(["a", "b", "x"]));
        Assert.NotEqual(baseline, ContentVersion.Of(["a", "b"]));
        Assert.NotEqual(baseline, ContentVersion.Of(["a", "b", "c", "d"]));
    }

    [Fact]
    public void Parts_are_framed_so_concatenation_cannot_make_two_different_rows_collide()
    {
        Assert.NotEqual(ContentVersion.Of(["ab", "c"]), ContentVersion.Of(["a", "bc"]));
        Assert.NotEqual(ContentVersion.Of(["a;b"]), ContentVersion.Of(["a", "b"]));
        Assert.NotEqual(ContentVersion.Of(["1:a"]), ContentVersion.Of(["a"]));
    }

    [Fact]
    public void A_missing_part_is_not_an_empty_one() =>
        Assert.NotEqual(ContentVersion.Of([null]), ContentVersion.Of([""]));

    [Fact]
    public void The_order_of_parts_matters() =>
        Assert.NotEqual(ContentVersion.Of(["a", "b"]), ContentVersion.Of(["b", "a"]));

    [Fact]
    public void Text_beyond_ascii_is_hashed_by_its_content()
    {
        Assert.Equal(ContentVersion.Of(["Zoë 注射"]), ContentVersion.Of(["Zoë 注射"]));
        Assert.NotEqual(ContentVersion.Of(["Zoë"]), ContentVersion.Of(["Zoe"]));
    }

    [Fact]
    public void The_etag_is_a_strong_quoted_tag_with_its_own_prefix()
    {
        var tag = ContentVersion.Of(["a"]).ToETag();

        Assert.Matches("^\"c-[0-9a-f]{32}\"$", tag);
        Assert.DoesNotContain("W/", tag);
    }

    [Fact]
    public void A_stamps_tag_is_never_a_content_tag_and_the_other_way_round()
    {
        var stamp = ConcurrencyToken.From(new DateTime(2026, 1, 2, 3, 4, 5)).ToETag();
        var content = ContentVersion.Of(["a"]).ToETag();

        Assert.Empty(ContentVersion.ParseIfMatch(stamp));
        Assert.Empty(ConcurrencyToken.ParseIfMatch(content));
    }

    [Fact]
    public void A_tag_the_service_issued_parses_back_to_the_version_it_came_from()
    {
        var version = ContentVersion.Of(["a", null, "c"]);

        Assert.Equal([version], ContentVersion.ParseIfMatch(version.ToETag()));
    }

    [Fact]
    public void A_list_of_tags_yields_every_readable_one_and_skips_the_rest()
    {
        var one = ContentVersion.Of(["1"]);
        var two = ContentVersion.Of(["2"]);

        var parsed = ContentVersion.ParseIfMatch($"{one.ToETag()}, garbage, W/{two.ToETag()}, {two.ToETag()}");

        Assert.Equal([one, two], parsed);   // the weak form of the second never matches under strong comparison, the plain one does
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("*")]
    [InlineData("\"c-xyz\"")]
    [InlineData("\"c-0123456789abcdef0123456789abcdeg\"")]
    [InlineData("c-0123456789abcdef0123456789abcdef")]
    [InlineData("\"0123456789abcdef0123456789abcdef\"")]
    public void Anything_unreadable_matches_nothing(string? header) =>
        Assert.Empty(ContentVersion.ParseIfMatch(header));

    [Fact]
    public void A_default_version_has_no_tag_to_give() =>
        Assert.Throws<InvalidOperationException>(() => default(ContentVersion).ToETag());

    [Fact]
    public void Tags_are_compared_case_insensitively_for_their_hex_digits()
    {
        var version = ContentVersion.Of(["a"]);
        var upper = version.ToETag().ToUpperInvariant().Replace("C-", "c-");

        Assert.Equal([version], ContentVersion.ParseIfMatch(upper));
    }
}
