using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.SoapNotes;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The adapter side of <see cref="ISoapNoteRegistry"/> and the anti-corruption layer for a SOAP write (LEG-2, ADR 0073): one transaction that writes what legacy's REST path writes (<c>form_soap</c> and its <c>forms</c>
/// row), plus the acting provider's username and group which that path leaves empty, the outbox row and the audit row. A note has no uuid, so no <c>uuid_registry</c> row exists.
/// </summary>
/// <remarks>
/// <b>Retry guard (ADR 0077).</b> Other registries find an earlier committed attempt by uuid. A note has no identity before its insert, so the attempt is given one: a fresh id, written into the
/// <c>SoapNoteRecorded</c> event in the same transaction as the note. If the execution strategy runs the transaction again (a transient fault, possibly at the instant of the commit reply, after the commit had
/// happened), the second run first looks for an event carrying this attempt's id and, if it is there, returns that note instead of writing another. The lookup runs only on a re-run, never on the first attempt,
/// so the usual path pays nothing for it.
/// </remarks>
public sealed class EfSoapNoteRegistry(ClinicalCareDbContext db, IAuditLog auditLog) : ISoapNoteRegistry
{
    public async Task RegisterAsync(SoapNote note, SoapNoteRecordedEvent @event, Func<long, AuditEntry> auditFor, CancellationToken ct)
    {
        var s = note.Sections;
        var pid = note.Encounter.Patient.Pid;
        var number = note.Encounter.Number;

        var attemptId = Guid.NewGuid();
        var runs = 0;

        var strategy = db.Database.CreateExecutionStrategy();
        var id = await strategy.ExecuteAsync(async () =>
        {
            if (runs++ > 0)
            {
                // A re-run: the earlier one may have committed before its acknowledgement was lost. If it did, its event carries this attempt's id.
                var earlier = await db.Database
                    .SqlQuery<long>(
                        $"""
                        SELECT CAST(JSON_VALUE(payload, '$.payload.noteId') AS SIGNED) AS Value FROM clinical_care_outbox
                        WHERE event_type = {SoapNoteRecordedEvent.EventType} AND JSON_VALUE(payload, '$.payload.attemptId') = {attemptId.ToString()}
                        LIMIT 1
                        """)
                    .ToListAsync(ct);
                if (earlier.Count == 1)
                    return earlier[0];
            }

            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var actor = await LegacyActor.ResolveAsync(db, note.ProviderId, ct);

            // Blank sections are stored as NULL, which is how the tolerant reader reads them back. authorized and activity are the flags legacy's own path writes.
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO form_soap (`date`, pid, `user`, groupname, authorized, activity, subjective, objective, assessment, plan)
                VALUES ({note.RecordedAt}, {pid}, {actor.Username}, {actor.GroupName}, 1, 1, {s.Subjective}, {s.Objective}, {s.Assessment}, {s.Plan})
                """, ct);
            var rowId = await db.Database.GetLastInsertIdAsync(ct);

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO forms (`date`, encounter, form_name, form_id, pid, `user`, groupname, authorized, deleted, formdir)
                VALUES ({note.RecordedAt}, {number}, 'SOAP', {rowId}, {pid}, {actor.Username}, {actor.GroupName}, 1, 0, 'soap')
                """, ct);

            var audit = auditFor(rowId);
            var recorded = @event with { NoteId = rowId, AttemptId = attemptId };
            var envelope = EventEnvelope.Serialize(
                SoapNoteRecordedEvent.EventType, recorded.RecordedAtUtc, $"soap:{rowId}", actor: audit.Actor.Subject, recorded);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO clinical_care_outbox (event_type, payload, occurred_at)
                VALUES ({SoapNoteRecordedEvent.EventType}, {envelope}, {recorded.RecordedAtUtc})
                """, ct);

            await auditLog.AppendAsync(audit, ct);   // last statement, same transaction (SEC-9)

            await transaction.CommitAsync(ct);
            return rowId;
        });

        note.AssignGeneratedId(id);
    }
}
