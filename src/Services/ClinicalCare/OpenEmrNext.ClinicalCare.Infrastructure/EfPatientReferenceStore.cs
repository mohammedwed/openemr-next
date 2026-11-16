using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IPatientReferenceStore"/>: Clinical Care's own two tables, written in one transaction.
/// <c>clinical_care_processed_events</c> is the idempotency record (EVT-3); <c>clinical_care_patient_ref</c> holds the
/// reference. Both are created by the seed script and owned by this service, so nothing here touches legacy.
/// </summary>
public sealed class EfPatientReferenceStore(ClinicalCareDbContext db) : IPatientReferenceStore
{
    public async Task<EventRecordingResult> RecordPatientRegisteredAsync(
        Guid eventId, PatientReference patient, DateTime? registeredAtUtc, CancellationToken ct)
    {
        var eventIdText = eventId.ToString();
        var uuidBytes = Rfc4122GuidConverter.ToRfc4122Bytes(patient.Uuid);
        var pid = patient.Pid;

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            // INSERT IGNORE against the primary key: zero rows affected means this event id was seen before. Doing this in
            // the same transaction as the effect is what makes "processed" and "applied" the same fact, so a crash between
            // them cannot leave an event marked done with nothing applied.
            var inserted = await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT IGNORE INTO clinical_care_processed_events (event_id, event_type) VALUES ({eventIdText}, 'PatientRegistered')", ct);
            if (inserted == 0)
                return EventRecordingResult.AlreadyProcessed;

            // A patient already known (the backfill got there first, or a different event named them) is left alone. A
            // different uuid claiming a pid already held is a real conflict and is allowed to fail loudly on the unique key.
            var known = await db.Database
                .SqlQuery<long>($"SELECT COUNT(*) AS Value FROM clinical_care_patient_ref WHERE patient_uuid = {uuidBytes}")
                .SingleAsync(ct);
            if (known == 0)
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO clinical_care_patient_ref (patient_uuid, pid, registered_at, source) VALUES ({uuidBytes}, {pid}, {registeredAtUtc}, 'event')", ct);
            }

            await transaction.CommitAsync(ct);
            return EventRecordingResult.Recorded;
        });
    }
}
