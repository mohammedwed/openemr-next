using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Application;

/// <summary>
/// Creates an encounter for a registered patient (slice 1). Thin by design (ARC-4): resolve what the rules need
/// through ports, let the domain decide, hand the result to <see cref="IEncounterRegistry"/> to commit with its
/// event. It knows nothing about EF Core, MariaDB, <c>forms</c>, <c>sequences</c> or the outbox table.
/// </summary>
public sealed class CreateEncounterUseCase(
    IPatientDirectory patients,
    IEncounterCodeLists codeLists,
    IFacilityDirectory facilities,
    IEncounterIdentityAllocator identities,
    IEncounterRegistry registry,
    TimeProvider clock)
{
    /// <exception cref="PatientNotFoundException">No patient has the requested uuid (CC-1). Nothing is persisted.</exception>
    /// <exception cref="ClinicalCareValidationException">One or more fields fail validation. Nothing is persisted and no number is allocated.</exception>
    public async Task<Encounter> ExecuteAsync(CreateEncounterCommand command, CancellationToken ct)
    {
        // CC-1: a missing patient is a 404, decided before any field is judged.
        var patient = await patients.FindAsync(command.PatientUuid, ct)
            ?? throw new PatientNotFoundException(command.PatientUuid);

        var classes = await codeLists.GetEncounterClassesAsync(ct);
        var facility = command.FacilityId > 0 ? await facilities.FindAsync(command.FacilityId, ct) : null;

        // Legacy datetime columns hold the server's local time, not UTC (LEG-11; unverified, see the slice doc
        // section 8), so the encounter date uses local time. The event carries UTC.
        var nowLocal = clock.GetLocalNow().DateTime;
        var nowUtc = clock.GetUtcNow().UtcDateTime;

        var creation = EncounterCreation.Create(
            patient, command.ClassCode, command.CalendarCategoryId, command.Date, command.Reason,
            command.FacilityId, command.PosCode, command.ProviderId, command.OnsetDate, command.Sensitivity,
            nowLocal, new EncounterReferenceData(classes, facility));

        // Only now, once the request is known to be valid, is a number taken from the shared counter.
        var identity = await identities.AllocateAsync(ct);
        var encounter = Encounter.Create(creation, identity);

        var @event = new EncounterCreatedEvent(
            encounter.Uuid, encounter.Number, patient.Uuid, patient.Pid, encounter.ClassCode, encounter.Date, nowUtc);

        await registry.RegisterAsync(encounter, @event, ct);

        return encounter;
    }
}
