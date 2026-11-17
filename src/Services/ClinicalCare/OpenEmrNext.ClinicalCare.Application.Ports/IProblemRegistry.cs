using OpenEmrNext.ClinicalCare.Domain.Problems;

namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>
/// What <c>RecordProblemUseCase</c> needs from persistence: a new problem, its <see cref="ProblemRecordedEvent"/> and its audit row committed
/// together, atomically (EVT-1, SEC-9), without the use case knowing how. "Together" includes the <c>uuid_registry</c> and <c>lists_touch</c> rows,
/// which are the adapter's concern (LEG-2).
/// </summary>
public interface IProblemRegistry
{
    /// <summary>Persists the problem, its event and its audit row in one transaction and records the database-assigned id on the problem. If it throws, nothing was written.</summary>
    Task RegisterAsync(Problem problem, ProblemRecordedEvent @event, AuditEntry audit, CancellationToken ct);
}
