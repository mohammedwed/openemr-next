using System.ComponentModel.DataAnnotations;

namespace OpenEmrNext.ClinicalCare.Composition;

/// <summary>
/// The shared secret a trusted internal caller must present (as the <c>X-Internal-Gateway-Secret</c>
/// header) to reach any ClinicalCare.Api endpoint (ADR 0055, applied here unchanged by ADR 0064 #6).
/// Bound from the <c>InternalGateway</c> section.
/// </summary>
/// <remarks>
/// This gates the service shut to anyone else; it does not verify *which* user is calling. The acting
/// provider is caller-supplied until identity forwarding exists (finding F-CC-1,
/// <c>docs/clinical-care/01-create-encounter-slice.md</c>).
/// </remarks>
public sealed class InternalGatewayOptions
{
    /// <summary>The configuration section name, <c>InternalGateway</c>.</summary>
    public const string SectionName = "InternalGateway";

    /// <summary>The shared secret. Required: a missing value fails startup instead of silently allowing calls.</summary>
    [Required]
    public string Secret { get; set; } = "";
}
