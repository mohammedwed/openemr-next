using Microsoft.Extensions.Logging;

namespace OpenEmrNext.AuthFacade.Api.Audit;

/// <summary>
/// The audit trail for authentication outcomes and BFF proxy access (ADR 0042): who did what, when — never
/// a credential, a token, or a request/response body. Scoped to login outcomes and the BFF proxy boundary,
/// not every authenticated endpoint: the BFF proxy is where real OpenEMR data actually flows, which is what
/// a HIPAA-style audit trail cares about. <c>/me</c> itself returns no PHI, just an identity check, and
/// fires on every dashboard load — too high-volume to be worth the noise for what it would add.
/// </summary>
internal static partial class AuditEvents
{
    [LoggerMessage(LogLevel.Information, "Login succeeded for subject {Subject}")]
    internal static partial void LoginSucceeded(this ILogger logger, string subject);

    /// <param name="logger"></param>
    /// <param name="reason">
    /// A short, fixed internal label for why — never OpenEMR's own error text or an exception message, the
    /// same "generic reason, never leak upstream text" principle <c>/callback</c>'s own browser-facing
    /// <c>?error=</c> code already follows (ADR 0026).
    /// </param>
    [LoggerMessage(LogLevel.Information, "Login failed: {Reason}")]
    internal static partial void LoginFailed(this ILogger logger, string reason);

    [LoggerMessage(LogLevel.Information, "Subject {Subject} called {Method} {Path} -> {StatusCode}")]
    internal static partial void ProxyAccess(this ILogger logger, string subject, string method, string path, int statusCode);
}
