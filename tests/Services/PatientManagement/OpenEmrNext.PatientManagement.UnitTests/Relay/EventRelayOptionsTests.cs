using System.ComponentModel.DataAnnotations;
using OpenEmrNext.PatientManagement.Composition;

namespace OpenEmrNext.PatientManagement.UnitTests.Relay;

/// <summary>OPS-3: the relay is off unless enabled, and when it is on a missing target or secret fails startup instead of silently dropping events.</summary>
public class EventRelayOptionsTests
{
    private static List<ValidationResult> Validate(EventRelayOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }

    private static EventRelayOptions Enabled() =>
        new() { Enabled = true, BaseUrl = "http://clinical-care:8080", Secret = "s3cret" };

    [Fact]
    public void A_disabled_relay_needs_no_configuration_at_all() => Assert.Empty(Validate(new EventRelayOptions()));

    [Fact]
    public void A_fully_configured_enabled_relay_is_valid() => Assert.Empty(Validate(Enabled()));

    [Theory]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("ftp://clinical-care")]
    [InlineData("/relative")]
    public void An_enabled_relay_without_a_usable_target_fails_validation(string url)
    {
        var options = Enabled();
        options.BaseUrl = url;

        Assert.Contains(Validate(options), r => r.MemberNames.Contains(nameof(EventRelayOptions.BaseUrl)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_enabled_relay_without_a_secret_fails_validation(string secret)
    {
        var options = Enabled();
        options.Secret = secret;

        Assert.Contains(Validate(options), r => r.MemberNames.Contains(nameof(EventRelayOptions.Secret)));
    }

    [Fact]
    public void A_lease_not_longer_than_the_http_timeout_is_rejected_because_a_slow_delivery_could_be_claimed_twice()
    {
        var options = Enabled();
        options.LeaseSeconds = 10;
        options.HttpTimeoutSeconds = 10;

        Assert.Contains(Validate(options), r => r.MemberNames.Contains(nameof(EventRelayOptions.LeaseSeconds)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public void The_batch_size_is_bounded(int batch)
    {
        var options = Enabled();
        options.BatchSize = batch;

        Assert.NotEmpty(Validate(options));
    }
}
