using System.ComponentModel.DataAnnotations;

namespace OpenEmrNext.AuthFacade.Api.OpenIddictClient;

/// <summary>
/// Governs where the browser authorization-code flow (ADR 0026) sends the browser, bound from the
/// <c>BrowserFlow</c> section.
/// </summary>
public sealed class BrowserFlowOptions : IValidatableObject
{
    /// <summary>The configuration section name, <c>BrowserFlow</c>.</summary>
    public const string SectionName = "BrowserFlow";

    /// <summary>
    /// Where <c>/login</c> and <c>/callback</c> send the browser when no <c>returnUrl</c> was given, or the
    /// one given cannot be used: it is missing, not an absolute <c>http</c>/<c>https</c> URL, its host is not
    /// in <see cref="ReturnUrlAllowedHosts"/>, or the flow cookie that would have carried it back cannot be
    /// read. Required.
    /// </summary>
    [Required]
    public string DefaultReturnUrl { get; set; } = "";

    /// <summary>
    /// The hosts a caller-supplied <c>returnUrl</c> may resolve to, compared case-insensitively. A
    /// <c>returnUrl</c> outside this list is replaced by <see cref="DefaultReturnUrl"/> rather than rejected
    /// outright. Must list at least one host.
    /// </summary>
    public string[] ReturnUrlAllowedHosts { get; set; } = [];

    /// <inheritdoc/>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ReturnUrlAllowedHosts.Length == 0)
        {
            yield return new ValidationResult(
                "BrowserFlow:ReturnUrlAllowedHosts must list at least one host.",
                [nameof(ReturnUrlAllowedHosts)]);
        }

        if (!string.IsNullOrEmpty(DefaultReturnUrl) &&
            !Uri.TryCreate(DefaultReturnUrl, UriKind.Absolute, out _))
        {
            yield return new ValidationResult(
                "BrowserFlow:DefaultReturnUrl must be an absolute URL.",
                [nameof(DefaultReturnUrl)]);
        }
    }
}
