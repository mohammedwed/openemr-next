using System.ComponentModel.DataAnnotations;

namespace OpenEmrNext.AuthFacade.Infrastructure.OpenEmr;

/// <summary>Configuration for the OpenEMR connection, bound from the <c>OpenEmr</c> section.</summary>
public sealed class OpenEmrOptions
{
    /// <summary>The configuration section name, <c>OpenEmr</c>.</summary>
    public const string SectionName = "OpenEmr";

    /// <summary>The base URL of the OpenEMR instance, used to reach its endpoints and JWKS. Required.</summary>
    [Required]
    public string BaseUrl { get; set; } = "";

    /// <summary>
    /// The issuer string OpenEMR writes into its tokens, for example <c>https://localhost:9300/oauth2/default</c>.
    /// It can differ from <see cref="BaseUrl"/> when the service reaches OpenEMR over a different network. Required.
    /// </summary>
    [Required]
    public string Issuer { get; set; } = "";

    /// <summary>The OAuth client ID this service uses with OpenEMR. Required.</summary>
    [Required]
    public string ClientId { get; set; } = "";

    /// <summary>The OAuth client secret this service uses with OpenEMR. Required.</summary>
    [Required]
    public string ClientSecret { get; set; } = "";

    /// <summary>The OAuth scope requested in the password grant. Defaults to <c>openid api:oemr</c>.</summary>
    public string Scope { get; set; } = "openid api:oemr";

    /// <summary>
    /// Accepts any OpenEMR server certificate. Off by default. Set only in development settings,
    /// because OpenEMR's development certificate is self-signed.
    /// </summary>
    public bool AcceptAnyServerCertificate { get; set; }
}
