using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IIssueDeleter"/> and the anti-corruption layer for deleting a problem or an allergy (LEG-2, ADR 0080): one transaction that locks the row, checks it is still the version the caller
/// read, removes the encounter links and the row, and writes the outbox row and the audit row. It does what legacy's <c>deleter.php</c> does for an issue (its links, then the <c>lists</c> row) and not the part that
/// is not ours (<c>lists_medication</c> belongs to medications).
/// </summary>
/// <remarks>
/// <para>The row is reached only by its uuid <b>and</b> its <c>type</c>, as every statement on <c>lists</c> is (an architecture test enforces it): <c>lists</c> holds every issue type, and this service has no business
/// removing a medication or a surgery. The service's database user holds <c>DELETE</c> on the whole table because MariaDB cannot grant it by column or by value, so that restriction is in this code and pinned by tests
/// that try to reach a row of another type.</para>
/// <para>The version is the row's <c>modifydate</c> stamp, read under <c>FOR UPDATE</c> in the same transaction, so a legacy edit between the caller's read and this delete is a 412 and never silently deleted
/// (LEG-10), and two deletions of one issue cannot both succeed. Legacy's own deletion writes the whole deleted row into its log; this service's audit row names the record and no clinical text.</para>
/// </remarks>
public sealed class EfIssueDeleter(ClinicalCareDbContext db, IAuditLog auditLog) : IIssueDeleter
{
    public async Task<DeletionOutcome> DeleteAsync(IssueDeletionRequest request, CancellationToken ct)
    {
        var type = request.Kind == DeletableIssue.Problem ? EfProblemRegistry.ProblemType : EfAllergyRegistry.AllergyType;
        var uuidBytes = Rfc4122GuidConverter.ToRfc4122Bytes(request.IssueUuid);
        var envelope = EventEnvelope.Serialize(
            request.Event.TypeName(), request.Event.DeletedAtUtc, request.IssueUuid.ToString(), actor: request.Audit.Actor.Subject, request.Event);

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var rows = await db.Database
                .SqlQuery<LockedRow>($"SELECT id AS Id, pid AS Pid, modifydate AS Stamp FROM lists WHERE uuid = {uuidBytes} AND type = {type} FOR UPDATE")
                .ToListAsync(ct);
            if (rows.Count == 0)
            {
                await transaction.RollbackAsync(ct);
                return new DeletionOutcome(DeletionStatus.NotFound, null);
            }

            var row = rows[0];
            var current = ConcurrencyToken.From(row.Stamp).ToETag();
            if (!request.OfferedVersions.Contains(current, StringComparer.Ordinal))
            {
                await transaction.RollbackAsync(ct);
                return new DeletionOutcome(DeletionStatus.Conflict, current);
            }

            // The links first, as legacy does (its REST path leaves them behind), then the issue itself.
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM issue_encounter WHERE list_id = {row.Id} AND pid = {row.Pid}", ct);
            var removed = await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM lists WHERE id = {row.Id} AND uuid = {uuidBytes} AND type = {type}", ct);
            if (removed == 0)
            {
                await transaction.RollbackAsync(ct);
                return new DeletionOutcome(DeletionStatus.NotFound, null);
            }

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO clinical_care_outbox (event_type, payload, occurred_at)
                VALUES ({request.Event.TypeName()}, {envelope}, {request.Event.DeletedAtUtc})
                """, ct);

            // Last statement, same transaction: the deletion cannot commit without its audit row (SEC-9, ADR 0069 #3).
            await auditLog.AppendAsync(request.Audit, ct);

            await transaction.CommitAsync(ct);
            return new DeletionOutcome(DeletionStatus.Deleted, null);
        });
    }

    private sealed class LockedRow
    {
        public long Id { get; set; }
        public long Pid { get; set; }
        public DateTime Stamp { get; set; }
    }
}
