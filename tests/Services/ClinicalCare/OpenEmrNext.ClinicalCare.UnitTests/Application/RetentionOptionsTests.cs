using System.ComponentModel.DataAnnotations;
using OpenEmrNext.ClinicalCare.Composition;

namespace OpenEmrNext.ClinicalCare.UnitTests.Application;

/// <summary>OPS-3 for retention: the defaults are valid, and a nonsensical setting fails startup instead of silently keeping or deleting the wrong things.</summary>
public class RetentionOptionsTests
{
    private static List<ValidationResult> Validate(RetentionOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void The_defaults_are_valid_and_are_the_documented_ones()
    {
        var defaults = new RetentionOptions();

        Assert.Empty(Validate(defaults));
        Assert.True(defaults.Enabled);
        Assert.Equal(30, defaults.ProcessedEventDays);
        Assert.Equal(30, defaults.PublishedOutboxDays);
        Assert.Equal(365, defaults.UnpublishedOutboxDays);   // long on purpose: nothing consumes these events yet
    }

    [Theory]
    [InlineData(nameof(RetentionOptions.ProcessedEventDays), 0)]
    [InlineData(nameof(RetentionOptions.PublishedOutboxDays), -1)]
    [InlineData(nameof(RetentionOptions.UnpublishedOutboxDays), 3651)]
    [InlineData(nameof(RetentionOptions.IntervalHours), 0)]
    [InlineData(nameof(RetentionOptions.BatchSize), 9)]
    [InlineData(nameof(RetentionOptions.BatchSize), 10_001)]
    public void A_value_outside_its_range_fails_validation(string property, int value)
    {
        var options = new RetentionOptions();
        typeof(RetentionOptions).GetProperty(property)!.SetValue(options, value);

        Assert.Contains(Validate(options), r => r.MemberNames.Contains(property));
    }
}
