using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class OauthClient
{
    public string ClientId { get; set; } = null!;

    public string? ClientRole { get; set; }

    public string ClientName { get; set; } = null!;

    public string? ClientSecret { get; set; }

    public string? RegistrationToken { get; set; }

    public string? RegistrationUriPath { get; set; }

    public DateTime? RegisterDate { get; set; }

    public DateTime? RevokeDate { get; set; }

    public string? Contacts { get; set; }

    public string? RedirectUri { get; set; }

    public string? GrantTypes { get; set; }

    public string? Scope { get; set; }

    public string? UserId { get; set; }

    public string? SiteId { get; set; }

    public bool? IsConfidential { get; set; }

    public string? LogoutRedirectUris { get; set; }

    public string? JwksUri { get; set; }

    public string? Jwks { get; set; }

    public string? InitiateLoginUri { get; set; }

    public string? Endorsements { get; set; }

    public string? PolicyUri { get; set; }

    public string? TosUri { get; set; }

    public bool IsEnabled { get; set; }

    public bool SkipEhrLaunchAuthorizationFlow { get; set; }

    /// <summary>
    /// 0=none, 1=evidence-based,2=predictive
    /// </summary>
    public byte DsiType { get; set; }
}
