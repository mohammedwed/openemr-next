using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Domain;

namespace OpenEmrNext.PatientManagement.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IPatientRegistry"/>: translates "register this patient and record this event,
/// atomically" into EF Core terms (ADR 0049) -- add the patient and the outbox row, one <c>SaveChangesAsync</c> -- plus the
/// <c>uuid_registry</c> row legacy's own <c>UuidRegistry::createUuid</c> inserts for every uuid it hands out (finding
/// F-CC-3). All three are written in **one transaction**, so the patient either exists completely or not at all.
/// Application never sees any of this; it only ever sees the port.
/// </summary>
public sealed class EfPatientRegistry(PatientManagementDbContext db, IAuditLog auditLog) : IPatientRegistry
{
    public async Task RegisterAsync(Patient patient, PatientRegisteredEvent @event, AuditEntry audit, CancellationToken ct)
    {
        var uuidBytes = Rfc4122GuidConverter.ToRfc4122Bytes(patient.Uuid);

        // The context retries transient failures, which replays the whole delegate; entities added by an attempt that
        // failed after SaveChanges must not be added a second time, so tracking is cleared at the start of each attempt.
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            db.Patients.Add(patient);
            db.OutboxMessages.Add(OutboxMessage.For(
                PatientRegisteredEvent.EventType, JsonSerializer.Serialize(@event), @event.RegisteredAtUtc));
            await db.SaveChangesAsync(ct);

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO uuid_registry (uuid, table_name, table_id, table_vertical, couchdb, document_drive, mapped, created)
                VALUES ({uuidBytes}, 'patient_data', 'id', '', '', 0, 0, NOW())
                """, ct);

            // Last statement, same transaction: the registration cannot commit without its audit row (SEC-9, ADR 0078).
            await auditLog.AppendAsync(audit, ct);

            await transaction.CommitAsync(ct);
        });
    }
}
