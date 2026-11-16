using OpenEmrNext.ClinicalCare.Domain;

namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>
/// What <c>CreateEncounterUseCase</c> needs from persistence: a new encounter and its
/// <see cref="EncounterCreatedEvent"/> committed together, atomically (CC-7, CC-11, EVT-1, ADR 0064), without
/// the use case knowing where or how. "Together" includes the legacy <c>forms</c> row and the
/// <c>uuid_registry</c> row, which are the adapter's concern, not the model's (LEG-2).
/// </summary>
public interface IEncounterRegistry
{
    /// <summary>
    /// Persists <paramref name="encounter"/> and <paramref name="event"/> in one transaction and records the
    /// database-assigned id on the encounter. If it throws, nothing was written.
    /// </summary>
    Task RegisterAsync(Encounter encounter, EncounterCreatedEvent @event, CancellationToken ct);
}
