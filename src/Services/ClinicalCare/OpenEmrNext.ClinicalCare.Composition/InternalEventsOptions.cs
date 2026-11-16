using System.ComponentModel.DataAnnotations;

namespace OpenEmrNext.ClinicalCare.Composition;

/// <summary>
/// The shared secret a trusted internal <b>producer</b> must present (as <c>X-Internal-Events-Secret</c>) to deliver an
/// event to <c>/internal/events</c>. Deliberately not <see cref="InternalGatewayOptions"/>: the gateway and the event
/// producers are different callers with different powers, so neither can present itself as the other (API-16, ADR 0066).
/// Bound from the <c>InternalEvents</c> section.
/// </summary>
public sealed class InternalEventsOptions
{
    /// <summary>The configuration section name, <c>InternalEvents</c>.</summary>
    public const string SectionName = "InternalEvents";

    /// <summary>The shared secret. Required: a missing value fails startup instead of silently accepting events from anyone.</summary>
    [Required]
    public string Secret { get; set; } = "";
}
