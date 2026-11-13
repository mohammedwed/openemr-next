namespace OpenEmrNext.PatientManagement.Application.Ports;

/// <summary>
/// The outbox payload for <c>PatientRegistered</c> -- a deliberately curated set of fields, not the
/// whole <c>patient_data</c> row (docs/m4-domain-notes.md), since nothing downstream needs more than
/// this yet.
/// </summary>
public sealed record PatientRegisteredEvent(Guid Uuid, long Pid, string FirstName, string LastName, DateTime RegisteredAtUtc)
{
    public const string EventType = "PatientRegistered";
}
