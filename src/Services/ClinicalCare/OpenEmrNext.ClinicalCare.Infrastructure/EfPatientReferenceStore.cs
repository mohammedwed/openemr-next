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

            // Conflicts are detected here, by name, not left to the unique key: a database exception is a 500, and a 500 tells the
            // relay to retry an event that can never succeed (review finding R-9). Throwing inside the transaction rolls back the
            // processed-event row too, so a corrected redelivery is still applied.
            var heldUnderThisUuid = (await db.Database
                .SqlQuery<long>($"SELECT pid AS Value FROM clinical_care_patient_ref WHERE patient_uuid = {uuidBytes}")
                .ToListAsync(ct)).Select(p => (long?)p).FirstOrDefault();
            var uuidHoldingThisPid = (await db.Database
                .SqlQuery<byte[]>($"SELECT patient_uuid AS Value FROM clinical_care_patient_ref WHERE pid = {pid}")
                .ToListAsync(ct)).FirstOrDefault();

            if (uuidHoldingThisPid is not null && Rfc4122GuidConverter.FromRfc4122Bytes(uuidHoldingThisPid) != patient.Uuid)
                throw new PatientReferenceConflictException(
                    $"Pid {pid} is already held by a different patient; patient {patient.Uuid} cannot also have it.");

            if (heldUnderThisUuid is { } existingPid && existingPid != pid)
                throw new PatientReferenceConflictException(
                    $"Patient {patient.Uuid} is already known with pid {existingPid}; the event says {pid}.");

            // A patient already known with the same pid (the backfill got there first, or a different event named them) is left alone.
            if (heldUnderThisUuid is null)
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO clinical_care_patient_ref (patient_uuid, pid, registered_at, source) VALUES ({uuidBytes}, {pid}, {registeredAtUtc}, 'event')", ct);
            }

            await transaction.CommitAsync(ct);
            return EventRecordingResult.Recorded;
        });
    }
}
