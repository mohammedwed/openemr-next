using System.ComponentModel.DataAnnotations;

namespace OpenEmrNext.AuthFacade.Infrastructure.OpenEmr;

public sealed class OpenEmrOptions
{
    public const string SectionName = "OpenEmr";

    [Required]
    public string BaseUrl { get; set; } = "";

    [Required]
    public string ClientId { get; set; } = "";

    [Required]
    public string ClientSecret { get; set; } = "";

    public string Scope { get; set; } = "openid api:oemr";
}
