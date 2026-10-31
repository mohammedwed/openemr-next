using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

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

    /// <summary>
    /// The redirect URI registered for this client (ADR 0024), for example
    /// <c>http://localhost:7128/callback</c>. Used both to build the authorize URL at <c>/login</c> and to
    /// redeem the code at <c>/callback</c>; it must match the value OpenEMR has on file for the client exactly.
    /// Required.
    /// </summary>
    [Required]
    public string RedirectUri { get; set; } = "";

    /// <summary>The OAuth scope requested in the password grant. Defaults to <c>openid api:oemr</c>.</summary>
    public string Scope { get; set; } = "openid api:oemr";

    /// <summary>
    /// Accepts any OpenEMR server certificate. Off by default. Set only in development settings,
    /// because OpenEMR's development certificate is self-signed. The API host refuses to start with
    /// this set outside the Development environment.
    /// </summary>
    public bool AcceptAnyServerCertificate { get; set; }

    /// <summary>
    /// Settings for the circuit breakers that guard calls to OpenEMR. The defaults open after half of at
    /// least five calls in 30 seconds fail, and keep the circuit open for 30 seconds.
    /// </summary>
    [ValidateObjectMembers]
    public CircuitBreakerOptions CircuitBreaker { get; set; } = new();
}
