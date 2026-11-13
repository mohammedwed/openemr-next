using System.Text.Json;
using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Domain;

namespace OpenEmrNext.PatientManagement.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IPatientRegistry"/>: translates "register this patient and record
/// this event, atomically" into EF Core terms -- add both to <see cref="PatientManagementDbContext"/>,
/// one <see cref="PatientManagementDbContext.SaveChangesAsync"/> call (ADR 0049). Application never
/// sees any of this; it only ever sees the port.
/// </summary>
public sealed class EfPatientRegistry(PatientManagementDbContext db) : IPatientRegistry
{
    public async Task RegisterAsync(Patient patient, PatientRegisteredEvent @event, CancellationToken ct)
    {
        db.Patients.Add(patient);
        db.OutboxMessages.Add(OutboxMessage.For(
            PatientRegisteredEvent.EventType, JsonSerializer.Serialize(@event), @event.RegisteredAtUtc));
        await db.SaveChangesAsync(ct);
    }
}
