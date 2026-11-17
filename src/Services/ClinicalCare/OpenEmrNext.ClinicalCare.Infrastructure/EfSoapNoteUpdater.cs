using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The adapter side of <see cref="ISoapNoteUpdater"/> (ADR 0072, 0073). <c>form_soap</c> has no stamp, so the version is a hash of the four sections, verified <b>under a row lock</b>: lock the note (matched through its
/// encounter's <c>forms</c> row, so another encounter's or patient's note is simply not found), re-read it with the reader's own mapping, compare with the caller's version, then write. The write changes the four
/// sections and nothing else: not <c>date</c> or <c>pid</c> (which legacy's own update rewrites), not who wrote it, not the <c>forms</c> row.
/// </summary>
public sealed class EfSoapNoteUpdater(ClinicalCareDbContext db, IAuditLog auditLog) : ISoapNoteUpdater
{
    public async Task<ContentUpdateOutcome> UpdateAsync(SoapNoteUpdateRequest request, CancellationToken ct)
    {
        var s = request.Sections;
        var (noteId, number, pid) = (request.NoteId, request.EncounterNumber, request.PatientPid);
        var envelope = EventEnvelope.Serialize(
            SoapNoteCorrectedEvent.EventType, request.Event.CorrectedAtUtc, $"soap:{noteId}", actor: "unknown", request.Event);

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var locked = await db.Database
                .SqlQuery<long>(
                    $"""
                    SELECT s.id AS Value FROM form_soap s
                    WHERE s.id = {noteId}
                      AND EXISTS (SELECT 1 FROM forms f WHERE f.form_id = s.id AND f.formdir = 'soap' AND f.deleted = 0 AND f.encounter = {number} AND f.pid = {pid})
                    FOR UPDATE
                    """)
                .ToListAsync(ct);
            var current = locked.Count == 0 ? null : await EfSoapNoteReader.FindAsync(db, number, pid, noteId, ct);
            if (current is null)
            {
                await transaction.RollbackAsync(ct);
                return new ContentUpdateOutcome(UpdateStatus.NotFound, null);
            }

            var currentVersion = current.Version();
            if (currentVersion != request.Expected)
            {
                await transaction.RollbackAsync(ct);
                return new ContentUpdateOutcome(UpdateStatus.Conflict, currentVersion);
            }

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE form_soap SET subjective = {s.Subjective}, objective = {s.Objective}, assessment = {s.Assessment}, plan = {s.Plan} WHERE id = {noteId} AND pid = {pid}", ct);

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO clinical_care_outbox (event_type, payload, occurred_at)
                VALUES ({SoapNoteCorrectedEvent.EventType}, {envelope}, {request.Event.CorrectedAtUtc})
                """, ct);
            await auditLog.AppendAsync(request.Audit, ct);   // same transaction: no correction without its audit row (SEC-9)

            await transaction.CommitAsync(ct);

            var after = await EfSoapNoteReader.FindAsync(db, number, pid, noteId, ct);
            return new ContentUpdateOutcome(UpdateStatus.Updated, after?.Version());
        });
    }
}
