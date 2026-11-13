using System.Text.Json;
using OpenEmrNext.PatientManagement.Domain;
using OpenEmrNext.PatientManagement.Infrastructure;

namespace OpenEmrNext.PatientManagement.Application;

/// <summary>
/// Registers a new patient: validates via <see cref="Patient.Register"/>, then persists the patient
/// row and its <c>PatientRegistered</c> outbox row in one transaction (one <see cref="PatientManagementDbContext.SaveChangesAsync"/>
/// call) -- the entire point of the outbox pattern (ADR 0049), achieved here rather than left to
/// whatever calls this use case.
/// </summary>
public sealed class RegisterPatientUseCase(PatientManagementDbContext db)
{
    /// <exception cref="PatientValidationException">One or more fields fail validation. Nothing is persisted.</exception>
    public async Task<Patient> ExecuteAsync(
        PatientRegistration registration, long registeredBy, DateTime registeredAtUtc, CancellationToken ct)
    {
        var patient = Patient.Register(registration, registeredBy, registeredAtUtc);

        var @event = new PatientRegisteredEvent(
            patient.Uuid, patient.Pid, patient.FirstName, patient.LastName, patient.RegisteredAtUtc);

        db.Patients.Add(patient);
        db.OutboxMessages.Add(OutboxMessage.For(
            PatientRegisteredEvent.EventType, JsonSerializer.Serialize(@event), registeredAtUtc));
        await db.SaveChangesAsync(ct);

        return patient;
    }
}
