using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.UnitTests.Domain;

/// <summary>Every list is bounded (API-6, DOM-11).</summary>
public class PageRequestTests
{
    [Fact]
    public void No_limit_or_offset_takes_the_defaults()
    {
        var page = PageRequest.Create(null, null);

        Assert.Equal(PageRequest.DefaultLimit, page.Limit);
        Assert.Equal(0, page.Offset);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void A_limit_at_either_bound_is_accepted(int limit) =>
        Assert.Equal(limit, PageRequest.Create(limit, 0).Limit);

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(101)]
    [InlineData(10_000)]
    public void A_limit_outside_1_to_100_is_rejected(int limit)
    {
        var ex = Assert.Throws<ClinicalCareValidationException>(() => PageRequest.Create(limit, 0));

        Assert.Contains("Limit must be between 1 and 100.", ex.Errors);
    }

    [Fact]
    public void A_negative_offset_is_rejected_and_both_errors_are_reported_together()
    {
        var ex = Assert.Throws<ClinicalCareValidationException>(() => PageRequest.Create(0, -1));

        Assert.Equal(2, ex.Errors.Count);
    }
}
