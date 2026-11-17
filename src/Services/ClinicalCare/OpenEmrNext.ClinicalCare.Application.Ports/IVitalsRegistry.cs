using OpenEmrNext.ClinicalCare.Domain.VitalSigns;

namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>
/// What <c>RecordVitalsUseCase</c> needs from persistence: new vitals and their <see cref="VitalsRecordedEvent"/>
/// committed together, atomically (CV-10, CV-11, CV-13, EVT-1), without the use case knowing how. "Together" includes the
/// legacy <c>forms</c> row and the <c>uuid_registry</c> row, which are the adapter's concern (LEG-2).
/// </summary>
public interface IVitalsRegistry
{
    /// <summary>Persists <paramref name="vitals"/>, <paramref name="event"/> and <paramref name="audit"/> in one transaction (SEC-9) and records the database-assigned id on the vitals. If it throws, nothing was written.</summary>
    Task RegisterAsync(Vitals vitals, VitalsRecordedEvent @event, AuditEntry audit, CancellationToken ct);
}
