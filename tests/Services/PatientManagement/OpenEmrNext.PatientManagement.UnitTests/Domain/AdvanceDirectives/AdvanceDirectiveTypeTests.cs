using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives;

namespace OpenEmrNext.PatientManagement.UnitTests.Domain.AdvanceDirectives;

public class AdvanceDirectiveTypeTests
{
    [Theory]
    [InlineData(AdvanceDirectiveType.LivingWill, "living_will")]
    [InlineData(AdvanceDirectiveType.DurablePowerOfAttorney, "durable_power_attorney")]
    [InlineData(AdvanceDirectiveType.DnrOrder, "dnr_order")]
    public void ToWireValue_and_TryParse_round_trip(AdvanceDirectiveType type, string wireValue)
    {
        Assert.Equal(wireValue, type.ToWireValue());
        Assert.Equal(type, AdvanceDirectiveTypeExtensions.TryParse(wireValue));
    }

    [Fact]
    public void TryParse_rejects_an_unknown_value() =>
        Assert.Null(AdvanceDirectiveTypeExtensions.TryParse("not-a-type"));

    [Theory]
    [InlineData(AdvanceDirectiveType.LivingWill, "Living Will")]
    [InlineData(AdvanceDirectiveType.DurablePowerOfAttorney, "Durable Power of Attorney")]
    [InlineData(AdvanceDirectiveType.DnrOrder, "Do Not Resuscitate Order")]
    public void ToDisplayName_matches_legacys_own_map(AdvanceDirectiveType type, string displayName) =>
        Assert.Equal(displayName, type.ToDisplayName());

    [Theory]
    [InlineData("Living Will - consent.pdf", AdvanceDirectiveType.LivingWill)]
    [InlineData("my living will scan.pdf", AdvanceDirectiveType.LivingWill)]
    [InlineData("Durable Power of Attorney - signed.pdf", AdvanceDirectiveType.DurablePowerOfAttorney)]
    [InlineData("DNR order.pdf", AdvanceDirectiveType.DnrOrder)]
    [InlineData("do not resuscitate form.pdf", AdvanceDirectiveType.DnrOrder)]
    public void ClassifyFromName_matches_keywords_in_the_name(string name, AdvanceDirectiveType expected) =>
        Assert.Equal(expected, AdvanceDirectiveTypeExtensions.ClassifyFromName(name));

    [Fact]
    public void ClassifyFromName_falls_back_to_the_category_when_the_name_does_not_match() =>
        Assert.Equal(
            AdvanceDirectiveType.LivingWill,
            AdvanceDirectiveTypeExtensions.ClassifyFromName("scan001.pdf", "Living Will"));

    [Fact]
    public void ClassifyFromName_returns_null_for_a_generic_advance_directive() =>
        Assert.Null(AdvanceDirectiveTypeExtensions.ClassifyFromName("scan001.pdf", "Advance Directive"));
}
