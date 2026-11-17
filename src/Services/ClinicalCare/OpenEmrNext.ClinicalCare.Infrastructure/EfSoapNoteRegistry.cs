using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.SoapNotes;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The adapter side of <see cref="ISoapNoteRegistry"/> and the anti-corruption layer for a SOAP write (LEG-2, ADR 0073): one transaction that writes what legacy's REST path writes (<c>form_soap</c> and its <c>forms</c>
/// row), plus the acting provider's username and group which that path leaves empty, the outbox row and the audit row. A note has no uuid, so no <c>uuid_registry</c> row exists.
/// </summary>
/// <remarks>
/// <b>No retry guard.</b> Other registries find an earlier committed attempt by uuid. A note has no identity before its insert, so a transaction retried after a lost commit acknowledgement could write a second note.
/// That needs a transient fault at the exact instant of the commit reply, and is stated as a limitation (docs/clinical-care/17-soap-notes-slice.md, L2); idempotency keys are deferred (F-CC-8).
/// </remarks>
public sealed class EfSoapNoteRegistry(ClinicalCareDbContext db, IAuditLog auditLog) : ISoapNoteRegistry
{
    public async Task RegisterAsync(SoapNote note, SoapNoteRecordedEvent @event, Func<long, AuditEntry> auditFor, CancellationToken ct)
    {
        var s = note.Sections;
        var pid = note.Encounter.Patient.Pid;
        var number = note.Encounter.Number;

        var strategy = db.Database.CreateExecutionStrategy();
        var id = await strategy.ExecuteAsync(async () =>
        {
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

            var recorded = @event with { NoteId = rowId };
            var envelope = EventEnvelope.Serialize(
                SoapNoteRecordedEvent.EventType, recorded.RecordedAtUtc, $"soap:{rowId}", actor: $"user:{note.ProviderId}", recorded);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO clinical_care_outbox (event_type, payload, occurred_at)
                VALUES ({SoapNoteRecordedEvent.EventType}, {envelope}, {recorded.RecordedAtUtc})
                """, ct);

            await auditLog.AppendAsync(auditFor(rowId), ct);   // last statement, same transaction (SEC-9)

            await transaction.CommitAsync(ct);
            return rowId;
        });

        note.AssignGeneratedId(id);
    }
}
