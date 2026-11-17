using OpenEmrNext.PatientManagement.Application.Ports;

namespace OpenEmrNext.PatientManagement.Application;

/// <summary>Builds the audit rows the write use cases hand to their adapters or append on a refusal (SEC-9, ADR 0078), so the actor, the request's origin and the clock are filled in one way.</summary>
internal static class Audit
{
    public static AuditEntry Success(AuditOrigin? origin, string action, string subject, IReadOnlyList<string>? changedFields = null) =>
        Entry(origin, action, subject, AuditOutcome.Success, changedFields);

    public static AuditEntry Failure(AuditOrigin? origin, string action, string subject) =>
        Entry(origin, action, subject, AuditOutcome.Failed, null);

    /// <summary>A patient addressed by its number rather than a uuid: the subject is a short key.</summary>
    public static string Patient(long pid) => $"patient:{pid}";

    private static AuditEntry Entry(AuditOrigin? origin, string action, string subject, AuditOutcome outcome, IReadOnlyList<string>? changedFields)
    {
        var o = origin ?? AuditOrigin.Unknown;
        return new AuditEntry(DateTime.UtcNow, o.Actor, action, subject, outcome, o.CorrelationId, o.SourceAddress, changedFields);
    }
}
