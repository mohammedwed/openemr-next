using System.Diagnostics;
using OpenEmrNext.ClinicalCare.Application;

namespace OpenEmrNext.ClinicalCare.Api.Endpoints;

/// <summary>
/// Builds the <see cref="RequestOrigin"/> the audit trail records (SEC-9) from an HTTP request, so no use case sees HTTP.
/// </summary>
/// <remarks>
/// The correlation id is the W3C trace id when the gateway forwarded one (so a log line, a trace and an audit row join), else
/// Kestrel's own request id. The address is the first <c>X-Forwarded-For</c> entry, else the connection's. Every request reaching an
/// endpoint has already passed the shared-secret check (ADR 0055), so the only sender is the gateway, whose forwarded header is
/// the one worth believing. It is still only an audit annotation: nothing makes a decision from it, and <c>AuditEntry</c> cuts it
/// to its column.
/// </remarks>
internal static class RequestOrigins
{
    public static RequestOrigin From(HttpContext http)
    {
        var correlation = Activity.Current is { IdFormat: ActivityIdFormat.W3C } activity
            ? activity.TraceId.ToString()
            : http.TraceIdentifier;

        var forwarded = http.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim();
        var address = string.IsNullOrWhiteSpace(forwarded) ? http.Connection.RemoteIpAddress?.ToString() : forwarded;

        return new RequestOrigin(correlation, address);
    }
}
