namespace OpenEmrNext.AuthFacade.Api.BrowserFlow;

/// <summary>
/// Resolves the <c>returnUrl</c> a caller supplies to <c>/login</c> against the configured allow-list
/// (ADR 0024), so an attacker-supplied value cannot turn the flow into an open redirect.
/// </summary>
internal static class ReturnUrlValidator
{
    /// <summary>
    /// Returns <paramref name="returnUrl"/> unchanged when it is an absolute <c>http</c>/<c>https</c> URL
    /// whose host is in <see cref="BrowserFlowOptions.ReturnUrlAllowedHosts"/>, otherwise
    /// <see cref="BrowserFlowOptions.DefaultReturnUrl"/>.
    /// </summary>
    public static string Resolve(string? returnUrl, BrowserFlowOptions options)
    {
        if (!string.IsNullOrEmpty(returnUrl) &&
            Uri.TryCreate(returnUrl, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
            options.ReturnUrlAllowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
        {
            return returnUrl;
        }

        return options.DefaultReturnUrl;
    }
}
