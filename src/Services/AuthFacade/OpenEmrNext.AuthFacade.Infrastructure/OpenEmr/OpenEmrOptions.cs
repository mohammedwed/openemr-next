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

    /// <summary>
    /// The redirect URI registered for this client (ADR 0024), for example
    /// <c>http://localhost:7128/callback</c>. Used both to build the authorize URL at <c>/login</c> and to
    /// redeem the code at <c>/callback</c>; it must match the value OpenEMR has on file for the client exactly.
    /// Required.
    /// </summary>
    [Required]
    public string RedirectUri { get; set; } = "";
}
