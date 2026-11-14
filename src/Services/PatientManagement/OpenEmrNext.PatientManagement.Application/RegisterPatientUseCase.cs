using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Domain;
using OpenEmrNext.PatientManagement.Domain.ValueObjects;

namespace OpenEmrNext.PatientManagement.Application;

/// <summary>
/// Registers a new patient: validates via <see cref="Patient.Register"/>, then hands the patient and
/// its <see cref="PatientRegisteredEvent"/> to <see cref="IPatientRegistry"/> -- the port, not a
/// concrete database -- to commit together. This class knows nothing about EF Core, MariaDB, or the
/// outbox table; that is entirely the adapter's job (Infrastructure's <c>EfPatientRegistry</c>).
/// </summary>
public sealed class RegisterPatientUseCase(IPatientRegistry registry)
{
    /// <exception cref="PatientValidationException">One or more fields fail validation. Nothing is persisted.</exception>
    public async Task<Patient> ExecuteAsync(
        PatientRegistration registration, long registeredBy, DateTime registeredAtUtc, CancellationToken ct)
    {
        var patient = Patient.Register(registration, registeredBy, registeredAtUtc);

        var @event = new PatientRegisteredEvent(
            patient.Uuid, patient.Pid, patient.FirstName, patient.LastName, patient.RegisteredAtUtc);

        await registry.RegisterAsync(patient, @event, ct);

        return patient;
    }
}
