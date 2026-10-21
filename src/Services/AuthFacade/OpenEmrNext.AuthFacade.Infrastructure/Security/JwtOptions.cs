using System.ComponentModel.DataAnnotations;

namespace OpenEmrNext.AuthFacade.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Key { get; set; } = "";

    public string Issuer { get; set; } = "openemr-next";

    public string Audience { get; set; } = "openemr-next-api";

    [Range(1, 1440)]
    public int LifetimeMinutes { get; set; } = 30;
}
