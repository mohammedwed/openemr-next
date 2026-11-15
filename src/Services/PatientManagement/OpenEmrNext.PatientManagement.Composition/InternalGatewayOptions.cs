using System.ComponentModel.DataAnnotations;

namespace OpenEmrNext.PatientManagement.Composition;

/// <summary>
/// The shared secret a trusted internal caller must present (as the <c>X-Internal-Gateway-Secret</c>
/// header) to reach any PatientManagement.Api endpoint. Bound from the <c>InternalGateway</c> section.
/// </summary>
/// <remarks>
/// This gates the service shut to anyone else; it does not verify *which* user is calling -- the Api
/// still trusts caller-supplied identity fields (<c>RegisteredBy</c>/<c>OwnerId</c>/
/// <c>AuthenticatorUserId</c>) exactly as before. Forwarding the real, authenticated user's identity
/// is a separate, not-yet-buildable gap: see the ADR this option's introduction is recorded in. No
/// caller currently attaches this header -- the Gateway (ADR 0030's eventual owner of routing to new
/// backend services) is the intended future caller, once M3 actually routes to this service.
/// </remarks>
public sealed class InternalGatewayOptions
{
    /// <summary>The configuration section name, <c>InternalGateway</c>.</summary>
    public const string SectionName = "InternalGateway";

    /// <summary>The shared secret. Required -- a missing value fails startup, not silently allows an unauthenticated call.</summary>
    [Required]
    public string Secret { get; set; } = "";
}
