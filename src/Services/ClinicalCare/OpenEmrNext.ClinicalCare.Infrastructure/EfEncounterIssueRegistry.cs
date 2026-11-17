using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.IssueLinks;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IEncounterIssueRegistry"/> and the anti-corruption layer for legacy's <c>issue_encounter</c> row (LEG-2, ADR 0070): a link is one transaction
/// holding the row, its <c>uuid_registry</c> row, the outbox row and the audit row; an unlink is one holding the delete, the outbox row and the audit row.
/// </summary>
/// <remarks>
/// <para>
/// <b>Idempotent, as legacy's is.</b> The table is unique on <c>(pid, list_id, encounter)</c>, so the link is an <c>INSERT IGNORE</c>: zero rows inserted means the link already
/// exists (or a concurrent caller just made it), and the stored one is returned with nothing else written, so no event and no audit row for a link that was not made.
/// The same reasoning makes a retry after a lost commit acknowledgement safe: it finds the link and reports it as existing, never writes a second one.
/// </para>
/// <para>
/// <b>The actor columns stay null.</b> <c>created_by</c> and <c>updated_by</c> are users.id, and this service does not know who is acting (ADR 0069 #5); a provider id the caller
/// named would be a guess written into a clinical table. <b>An unlink deletes the link row only.</b> Legacy also deletes the link's <c>uuid_registry</c> row, which here would
/// need <c>DELETE</c> on a table shared by every uuid in the system; the row left behind is harmless because uuids are never reused.
/// </para>
/// </remarks>
public sealed class EfEncounterIssueRegistry(ClinicalCareDbContext db, IAuditLog auditLog) : IEncounterIssueRegistry
{
    public async Task<LinkRegistration> LinkAsync(EncounterIssueLink link, IssueLinkedToEncounterEvent @event, AuditEntry audit, CancellationToken ct)
    {
        var uuidBytes = Rfc4122GuidConverter.ToRfc4122Bytes(link.Uuid);
        var envelope = EventEnvelope.Serialize(
            IssueLinkedToEncounterEvent.EventType, @event.LinkedAtUtc, link.EncounterUuid.ToString(), actor: audit.Actor.Subject, @event);
        var pid = link.Patient.Pid;
        var listId = link.Issue.Id;
        var number = link.EncounterNumber;
        var resolved = link.Resolved ? 1 : 0;

        var strategy = db.Database.CreateExecutionStrategy();
        var (created, id, storedUuid, storedResolved) = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var inserted = await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT IGNORE INTO issue_encounter (uuid, pid, list_id, encounter, resolved, created_by, updated_by)
                VALUES ({uuidBytes}, {pid}, {listId}, {number}, {resolved}, NULL, NULL)
                """, ct);

            if (inserted == 0)
            {
                // Already linked (or a concurrent caller just did it): report the stored link and write nothing else.
                var existing = await db.Database
                    .SqlQuery<LinkRow>(
                        $"SELECT id AS Id, uuid AS Uuid, resolved AS Resolved FROM issue_encounter WHERE pid = {pid} AND list_id = {listId} AND encounter = {number}")
                    .ToListAsync(ct);
                var row = existing.Single();
                return (false, row.Id, row.Uuid is { Length: 16 } u ? Rfc4122GuidConverter.FromRfc4122Bytes(u) : (Guid?)null, row.Resolved != 0);
            }

            var rowId = await db.Database.GetLastInsertIdAsync(ct);

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO uuid_registry (uuid, table_name, table_id, table_vertical, couchdb, document_drive, mapped, created)
                VALUES ({uuidBytes}, 'issue_encounter', 'id', '', '', 0, 0, NOW())
                """, ct);

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO clinical_care_outbox (event_type, payload, occurred_at)
                VALUES ({IssueLinkedToEncounterEvent.EventType}, {envelope}, {@event.LinkedAtUtc})
                """, ct);

            // Last statement, same transaction: a link cannot exist without its audit row (SEC-9, ADR 0069 #3).
            await auditLog.AppendAsync(audit, ct);

            await transaction.CommitAsync(ct);
            return (true, rowId, (Guid?)link.Uuid, link.Resolved);
        });

        if (created)
            link.AssignGeneratedId(id);

        return new LinkRegistration(created, storedUuid, storedResolved);
    }

    public async Task<bool> UnlinkAsync(UnlinkRequest request, CancellationToken ct)
    {
        var pid = request.PatientPid;
        var listId = request.Issue.Id;
        var number = request.EncounterNumber;
        var envelope = EventEnvelope.Serialize(
            IssueUnlinkedFromEncounterEvent.EventType, request.Event.UnlinkedAtUtc, request.EncounterUuid.ToString(), actor: request.Audit.Actor.Subject, request.Event);

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var removed = await db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM issue_encounter WHERE pid = {pid} AND list_id = {listId} AND encounter = {number}", ct);

            if (removed == 0)
                return false;   // nothing was linked, so nothing happened: no event and no audit row

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO clinical_care_outbox (event_type, payload, occurred_at)
                VALUES ({IssueUnlinkedFromEncounterEvent.EventType}, {envelope}, {request.Event.UnlinkedAtUtc})
                """, ct);

            await auditLog.AppendAsync(request.Audit, ct);

            await transaction.CommitAsync(ct);
            return true;
        });
    }

    private sealed class LinkRow
    {
        public long Id { get; set; }
        public byte[]? Uuid { get; set; }
        public int Resolved { get; set; }
    }
}
