using OpenEmrNext.ClinicalCare.Domain.VitalSigns;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>
/// The outbox payload for <c>VitalsCorrected</c>: the names of the measurements that changed, never their values (DOM-16, EVT-5; as <c>VitalsRecorded</c> names what was taken). A
/// consumer that needs the new readings asks the API.
/// </summary>
public sealed record VitalsCorrectedEvent(
    Guid VitalsUuid, Guid EncounterUuid, long EncounterNumber, Guid PatientUuid, long PatientPid, IReadOnlyList<string> ChangedMeasurements, DateTime CorrectedAtUtc)
{
    public const string EventType = "VitalsCorrected";
}

/// <summary>Everything one correction must commit together: the change, the version it is based on, the event and the audit row.</summary>
/// <param name="EncounterNumber">The encounter's number: the vitals belong to it through the <c>forms</c> row, and the update must not reach vitals of another encounter or patient.</param>
/// <param name="PatientPid">The patient the encounter is for, by legacy key.</param>
/// <param name="Expected">The version the caller read. The write applies only if the row still has it (LEG-10).</param>
public sealed record VitalsUpdateRequest(
    Guid VitalsUuid, long EncounterNumber, long PatientPid, ConcurrencyToken Expected, VitalsChange Change, VitalsCorrectedEvent Event, AuditEntry Audit);

/// <summary>
/// What <c>CorrectVitalsUseCase</c> needs from persistence: a conditional update of the measurement columns (ADR 0068), committed with its event and its audit row, atomically, and
/// reaching only vitals that belong to this encounter and have not been soft-deleted.
/// </summary>
public interface IVitalsUpdater
{
    /// <summary>Applies the change if and only if the row still has the expected version. Anything but <see cref="UpdateStatus.Updated"/> means nothing was written.</summary>
    Task<UpdateOutcome> UpdateAsync(VitalsUpdateRequest request, CancellationToken ct);
}
