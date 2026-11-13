using OpenEmrNext.PatientManagement.Domain;

namespace OpenEmrNext.PatientManagement.Application.Ports;

/// <summary>
/// What <c>RegisterPatientUseCase</c> needs from persistence: a new patient and its
/// <see cref="PatientRegisteredEvent"/> committed together, atomically (ADR 0049) -- without the use
/// case knowing anything about how, or where, that atomicity is actually achieved. The adapter
/// implementing this (Infrastructure's <c>EfPatientRegistry</c>) is the only thing that knows EF Core,
/// or this service's outbox table, exist.
/// </summary>
public interface IPatientRegistry
{
    Task RegisterAsync(Patient patient, PatientRegisteredEvent @event, CancellationToken ct);
}
