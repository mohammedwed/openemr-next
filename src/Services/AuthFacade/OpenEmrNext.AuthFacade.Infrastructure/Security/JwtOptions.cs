using System.ComponentModel.DataAnnotations;

namespace OpenEmrNext.AuthFacade.Infrastructure.Security;

/// <summary>Configuration for this service's own JWTs, bound from the <c>Jwt</c> section.</summary>
public sealed class JwtOptions
{
    /// <summary>The configuration section name, <c>Jwt</c>.</summary>
    public const string SectionName = "Jwt";

    /// <summary>The HMAC-SHA256 signing key. Required. Supplied by environment or user secrets, not appsettings.</summary>
    [Required]
    public string Key { get; set; } = "";

    /// <summary>The <c>iss</c> value written to and required in tokens. Required.</summary>
    [Required]
    public string Issuer { get; set; } = "";

    /// <summary>The <c>aud</c> value written to and required in tokens. Required.</summary>
    [Required]
    public string Audience { get; set; } = "";

    /// <summary>Token lifetime in minutes, from 1 to 1440. Required.</summary>
    [Range(1, 1440)]
    public int LifetimeMinutes { get; set; }
}
