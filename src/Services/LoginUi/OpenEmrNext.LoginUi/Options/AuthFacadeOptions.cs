using System.ComponentModel.DataAnnotations;

namespace OpenEmrNext.LoginUi.Options;

/// <summary>Configuration for reaching AuthFacade, bound from the <c>AuthFacade</c> section.</summary>
public sealed class AuthFacadeOptions
{
    /// <summary>The configuration section name, <c>AuthFacade</c>.</summary>
    public const string SectionName = "AuthFacade";

    /// <summary>
    /// The public base URL of the AuthFacade API, for example <c>http://localhost:7128</c>. The login
    /// button links to <c>{BaseUrl}/oidc/login</c>, which starts the browser authorization-code flow
    /// (ADR 0026) — the browser follows this link directly, so it must be the host-reachable address, not
    /// the Compose-internal <c>auth-facade</c> hostname. Required.
    /// </summary>
    [Required]
    public string BaseUrl { get; set; } = "";
}
