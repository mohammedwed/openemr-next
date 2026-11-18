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
public sealed class RegisterPatientUseCase(IPatientRegistry registry, IAuditLog auditLog)
{
    /// <summary>Builds the registration from raw fields (so the domain owns the rules), then registers. A field that fails validation writes a <c>failed</c> audit row and throws <see cref="PatientValidationException"/>.</summary>
    public async Task<Patient> ExecuteAsync(
        string firstName, string lastName, string sex, DateOnly dateOfBirth, string? email, string? trustedEmail,
        long registeredBy, DateTime registeredAtUtc, CancellationToken ct, AuditOrigin? origin = null)
    {
        PatientRegistration registration;
        try
        {
            registration = PatientRegistration.Create(firstName, lastName, sex, dateOfBirth, email, trustedEmail);
        }
        catch (PatientValidationException)
        {
            await AuditRefusalAsync(origin, ct);
            throw;
        }

        return await ExecuteAsync(registration, registeredBy, registeredAtUtc, ct, origin);
    }

    /// <summary>Registers the patient. The success audit row is committed with the patient (SEC-9, ADR 0078); a refusal writes a <c>failed</c> one.</summary>
    public async Task<Patient> ExecuteAsync(
        PatientRegistration registration, long registeredBy, DateTime registeredAtUtc, CancellationToken ct, AuditOrigin? origin = null)
    {
        Patient patient;
        try
        {
            patient = Patient.Register(registration, registeredBy, registeredAtUtc);
        }
        catch (PatientValidationException)
        {
            await AuditRefusalAsync(origin, ct);
            throw;
        }

        var @event = new PatientRegisteredEvent(
            patient.Uuid, patient.Pid, patient.FirstName, patient.LastName, patient.RegisteredAtUtc);
        await registry.RegisterAsync(patient, @event, Audit.Success(origin, AuditActions.PatientRegister, patient.Uuid.ToString()), ct);
        return patient;
    }

    private Task AuditRefusalAsync(AuditOrigin? origin, CancellationToken ct) =>
        auditLog.AppendAsync(Audit.Failure(origin, AuditActions.PatientRegister, "unregistered"), ct);
}
