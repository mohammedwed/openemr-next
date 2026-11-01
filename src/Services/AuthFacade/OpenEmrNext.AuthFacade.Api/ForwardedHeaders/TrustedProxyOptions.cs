using System.ComponentModel.DataAnnotations;
using System.Net;

namespace OpenEmrNext.AuthFacade.Api.ForwardedHeaders;

/// <summary>
/// The one network AuthFacade trusts to tell it the request's real scheme and client address, bound from
/// <c>ForwardedHeaders</c> (ADR 0039). In Compose, this is the gateway's own network — a request reaching
/// AuthFacade's own directly-published port cannot spoof either header, since it did not come from inside
/// that network.
/// </summary>
public sealed class TrustedProxyOptions : IValidatableObject
{
    /// <summary>The configuration section these options are bound from.</summary>
    public const string SectionName = "ForwardedHeaders";

    /// <summary>The trusted network, in CIDR notation, for example <c>172.28.0.0/16</c>. Required.</summary>
    [Required]
    public string TrustedNetworkCidr { get; set; } = "";

    /// <inheritdoc/>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrEmpty(TrustedNetworkCidr) && !IPNetwork.TryParse(TrustedNetworkCidr, out _))
        {
            yield return new ValidationResult(
                "ForwardedHeaders:TrustedNetworkCidr must be a valid CIDR network, for example 172.28.0.0/16.",
                [nameof(TrustedNetworkCidr)]);
        }
    }
}
