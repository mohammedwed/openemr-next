using System.Diagnostics;
using OpenEmrNext.PatientManagement.Api.Authentication;
using OpenEmrNext.PatientManagement.Application.Ports;

namespace OpenEmrNext.PatientManagement.Api.Endpoints;

/// <summary>Builds the audit origin of a request (SEC-9, ADR 0078): the verified user as the actor, the trace id that links the row to the logs, and the caller's address as the gateway forwarded it.</summary>
internal static class AuditOrigins
{
    public static AuditOrigin From(HttpContext http)
    {
        var correlation = Activity.Current is { IdFormat: ActivityIdFormat.W3C } activity
            ? activity.TraceId.ToString()
            : http.TraceIdentifier;

        var forwarded = http.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim();
        var address = string.IsNullOrWhiteSpace(forwarded) ? http.Connection.RemoteIpAddress?.ToString() : forwarded;

        var actor = http.VerifiedUser() is { } user
            ? new AuditActor(user.Uuid.ToString(), user.UserId, Verified: true)
            : AuditActor.Unknown;

        return new AuditOrigin(actor, correlation, address);
    }
}
