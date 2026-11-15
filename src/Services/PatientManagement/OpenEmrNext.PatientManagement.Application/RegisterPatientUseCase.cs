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
    /// <summary>
    /// Builds the <see cref="PatientRegistration"/> from raw facts first, then registers it -- the one
    /// place that sequence lives. Both <c>PatientEndpoints.RegisterPatient</c> and
    /// <c>FhirPatientEndpoints.CreatePatient</c> used to construct the registration themselves before
    /// calling <see cref="ExecuteAsync(PatientRegistration,long,DateTime,CancellationToken)"/>
    /// directly -- duplicated, identical orchestration across two Api files for two different input
    /// shapes (raw HTTP fields vs. FHIR-resource-extracted fields). Each endpoint still shapes its own
    /// error response for <see cref="PatientValidationException"/> (REST <c>ValidationProblem</c> vs.
    /// FHIR <c>OperationOutcome</c>) -- only the construct-then-register sequence moved here.
    /// </summary>
    /// <exception cref="PatientValidationException">One or more fields fail validation. Nothing is persisted.</exception>
    public Task<Patient> ExecuteAsync(
        string firstName, string lastName, string sex, DateOnly dateOfBirth, string? email, string? trustedEmail,
        long registeredBy, DateTime registeredAtUtc, CancellationToken ct) =>
        ExecuteAsync(
            PatientRegistration.Create(firstName, lastName, sex, dateOfBirth, email, trustedEmail),
            registeredBy, registeredAtUtc, ct);

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
