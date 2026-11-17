using OpenEmrNext.ClinicalCare.Application.Ports;

namespace OpenEmrNext.ClinicalCare.Application;

/// <summary>
/// The signed-in user behind a request, as the gateway verified it (ADR 0075): their OpenEMR uuid (the session's subject), the legacy <c>users.id</c> that uuid resolves to, and their username. Built by the API
/// from the forwarded identity; no use case reads a header.
/// </summary>
public sealed record VerifiedActor(Guid Uuid, long UserId, string? Username);

/// <summary>
/// Where a request came from, as far as the audit trail (SEC-9) records it: a correlation id to join log lines and audit rows, the caller's network address, and, when the gateway verified one, who the user was.
/// Built by the API from the HTTP request so no use case knows about HTTP; all are optional because a command can also come from a test or a future non-HTTP entry point.
/// </summary>
public sealed record RequestOrigin(string? CorrelationId, string? SourceAddress, VerifiedActor? Actor = null);

/// <summary>The audit actor for a request: the verified user when there is one, else what the caller named, else nobody (ADR 0069 #5, ADR 0075).</summary>
internal static class Actors
{
    /// <param name="origin">The request's origin, whose verified user wins.</param>
    /// <param name="namedProviderId">A provider id the caller named, used only when nobody was verified (a non-HTTP caller); a named provider is recorded as <b>unverified</b>.</param>
    public static AuditActor Of(RequestOrigin? origin, long? namedProviderId = null) =>
        origin?.Actor is { } actor ? new AuditActor(actor.Uuid.ToString(), actor.UserId, Verified: true)
        : namedProviderId is { } id ? new AuditActor($"user:{id}", id, Verified: false)
        : AuditActor.Unknown;
}
