using System.ComponentModel.DataAnnotations;

namespace OpenEmrNext.PatientManagement.Composition;

/// <summary>
/// The outbox relay's settings (EVT-7, ADR 0066). Bound from the <c>EventRelay</c> section. The relay is **off unless
/// enabled**: a host that has no consumer to deliver to (a test host, a developer's machine) simply does not run it, and
/// when it is on, a missing target or secret fails startup instead of silently dropping events (OPS-3).
/// </summary>
public sealed class EventRelayOptions : IValidatableObject
{
    /// <summary>The configuration section name, <c>EventRelay</c>.</summary>
    public const string SectionName = "EventRelay";

    /// <summary>Whether this host runs the relay.</summary>
    public bool Enabled { get; set; }

    /// <summary>The consumer's base address, for example <c>http://clinical-care:8080</c>. Required when enabled.</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>The secret the consumer's event endpoint requires (<c>X-Internal-Events-Secret</c>). Required when enabled.</summary>
    public string Secret { get; set; } = "";

    /// <summary>Seconds between passes over the outbox.</summary>
    [Range(1, 3600)]
    public int IntervalSeconds { get; set; } = 5;

    /// <summary>Rows claimed per pass.</summary>
    [Range(1, 500)]
    public int BatchSize { get; set; } = 20;

    /// <summary>Failed attempts before a row is dead-lettered.</summary>
    [Range(1, 100)]
    public int MaxAttempts { get; set; } = 8;

    /// <summary>Seconds a claimed row is leased; must exceed one delivery.</summary>
    [Range(5, 3600)]
    public int LeaseSeconds { get; set; } = 60;

    /// <summary>Seconds before one delivery is abandoned.</summary>
    [Range(1, 300)]
    public int HttpTimeoutSeconds { get; set; } = 10;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enabled)
            yield break;

        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            yield return new ValidationResult("EventRelay:BaseUrl must be an absolute http or https URL when the relay is enabled.", [nameof(BaseUrl)]);

        if (string.IsNullOrWhiteSpace(Secret))
            yield return new ValidationResult("EventRelay:Secret is required when the relay is enabled.", [nameof(Secret)]);

        if (LeaseSeconds <= HttpTimeoutSeconds)
            yield return new ValidationResult("EventRelay:LeaseSeconds must exceed HttpTimeoutSeconds, or a slow delivery could be claimed twice.", [nameof(LeaseSeconds)]);
    }
}
