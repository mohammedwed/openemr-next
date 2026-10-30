namespace OpenEmrNext.AuthFacade.Api.BrowserFlow;

/// <summary>
/// The error codes <c>/callback</c> appends to a failure redirect (ADR 0024). Always one of these three —
/// OpenEMR's own error text, a rejected-grant reason, or a verification failure never reaches the browser.
/// </summary>
internal static class BrowserFlowErrorCodes
{
    /// <summary>OpenEMR itself reported an error on the callback, for example the user declined consent.</summary>
    public const string AccessDenied = "access_denied";

    /// <summary>The flow cookie was missing, expired, or its <c>state</c> did not match the callback.</summary>
    public const string InvalidRequest = "invalid_request";

    /// <summary>Anything else: a rejected grant, an upstream fault, or a token that did not verify.</summary>
    public const string LoginFailed = "login_failed";
}
