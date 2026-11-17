using OpenEmrNext.ClinicalCare.Domain.Allergies;
using OpenEmrNext.ClinicalCare.Domain.Problems;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>
/// The outbox payload for <c>ProblemUpdated</c>: which fields changed, never their values (DOM-16, EVT-5). The patient keys are nullable because an update can
/// reach a legacy-created row whose <c>pid</c> matches no known patient.
/// </summary>
public sealed record ProblemUpdatedEvent(
    Guid ProblemUuid, Guid? PatientUuid, long? PatientPid, IReadOnlyList<string> ChangedFields, DateTime UpdatedAtUtc)
{
    public const string EventType = "ProblemUpdated";
}

/// <summary>The outbox payload for <c>AllergyUpdated</c>: which fields changed, never their values (DOM-16, EVT-5).</summary>
public sealed record AllergyUpdatedEvent(
    Guid AllergyUuid, Guid? PatientUuid, long? PatientPid, IReadOnlyList<string> ChangedFields, DateTime UpdatedAtUtc)
{
    public const string EventType = "AllergyUpdated";
}

/// <summary>Everything one problem update must commit together: the change, the version it is based on, the event and the audit row.</summary>
/// <param name="Expected">The version the caller read. The write applies only if the row still has it (LEG-10).</param>
public sealed record ProblemUpdateRequest(
    Guid ProblemUuid, ConcurrencyToken Expected, ProblemChange Change, ProblemUpdatedEvent Event, AuditEntry Audit);

/// <summary>Everything one allergy update must commit together: the change, the version it is based on, the event and the audit row.</summary>
/// <param name="Expected">The version the caller read. The write applies only if the row still has it (LEG-10).</param>
public sealed record AllergyUpdateRequest(
    Guid AllergyUuid, ConcurrencyToken Expected, AllergyChange Change, AllergyUpdatedEvent Event, AuditEntry Audit);

/// <summary>
/// What <c>UpdateProblemUseCase</c> needs from persistence: a conditional update of the columns a problem owns (ADR 0068, ADR 0070), committed with its event and
/// its audit row, atomically. Every statement is scoped to <c>type = 'medical_problem'</c>.
/// </summary>
public interface IProblemUpdater
{
    /// <summary>Applies the change if and only if the row still has the expected version. Anything but <see cref="UpdateStatus.Updated"/> means nothing was written.</summary>
    Task<UpdateOutcome> UpdateAsync(ProblemUpdateRequest request, CancellationToken ct);
}

/// <summary>
/// What <c>UpdateAllergyUseCase</c> needs from persistence: a conditional update of the columns an allergy owns (ADR 0068, ADR 0070), committed with its event and
/// its audit row, atomically. Every statement is scoped to <c>type = 'allergy'</c>.
/// </summary>
public interface IAllergyUpdater
{
    /// <summary>Applies the change if and only if the row still has the expected version. Anything but <see cref="UpdateStatus.Updated"/> means nothing was written.</summary>
    Task<UpdateOutcome> UpdateAsync(AllergyUpdateRequest request, CancellationToken ct);
}
