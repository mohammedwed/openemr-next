using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IProblemUpdater"/> (LEG-2, ADR 0068, ADR 0070): one transaction holding a conditional <c>UPDATE</c> of the columns a problem owns, the
/// <c>ProblemUpdated</c> outbox row and the audit row.
/// </summary>
/// <remarks>
/// The statement is conditional on the row's stamp (<c>modifydate = expected</c>) and on <c>type = 'medical_problem'</c>, so a legacy edit in the meantime is detected, a
/// row of another issue type can never be reached by its uuid, and no column outside the owned set is named (LEG-10). "Zero rows matched" means the stamp moved or the
/// row is gone (MySqlConnector counts matched rows, not changed ones); a second read tells the two apart. <c>verification</c> is a NOT NULL column, so an absent value is
/// the empty string. As for encounters, a lost commit acknowledgement is reported as a conflict on retry, never as a second write (slice doc, L2).
/// </remarks>
public sealed class EfProblemUpdater(ClinicalCareDbContext db, IAuditLog auditLog) : IProblemUpdater
{
    public async Task<UpdateOutcome> UpdateAsync(ProblemUpdateRequest request, CancellationToken ct)
    {
        var uuidBytes = Rfc4122GuidConverter.ToRfc4122Bytes(request.ProblemUuid);
        var change = request.Change;
        var expected = request.Expected.Stamp;
        var verification = change.Verification ?? "";
        var envelope = EventEnvelope.Serialize(
            ProblemUpdatedEvent.EventType, request.Event.UpdatedAtUtc, request.ProblemUuid.ToString(), actor: request.Audit.Actor.Subject, request.Event);

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var matched = await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE lists
                SET title = {change.Title}, begdate = {change.BeginDate}, enddate = {change.EndDate}, diagnosis = {change.Diagnosis},
                    outcome = {change.Outcome}, occurrence = {change.Occurrence}, referredby = {change.ReferredBy},
                    comments = {change.Comments}, verification = {verification}
                WHERE uuid = {uuidBytes} AND type = {EfProblemRegistry.ProblemType} AND modifydate = {expected}
                """, ct);

            if (matched == 0)
            {
                var current = await db.Database
                    .SqlQuery<DateTime>(
                        $"SELECT modifydate AS Value FROM lists WHERE uuid = {uuidBytes} AND type = {EfProblemRegistry.ProblemType}")
                    .ToListAsync(ct);

                return current.Count == 0
                    ? new UpdateOutcome(UpdateStatus.NotFound, null)
                    : new UpdateOutcome(UpdateStatus.Conflict, current[0]);
            }

            var stamp = (await db.Database
                .SqlQuery<DateTime>($"SELECT modifydate AS Value FROM lists WHERE uuid = {uuidBytes} AND type = {EfProblemRegistry.ProblemType}")
                .ToListAsync(ct)).Single();

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO clinical_care_outbox (event_type, payload, occurred_at)
                VALUES ({ProblemUpdatedEvent.EventType}, {envelope}, {request.Event.UpdatedAtUtc})
                """, ct);

            // Last statement, same transaction: the change cannot commit without its audit row (SEC-9, ADR 0069 #3).
            await auditLog.AppendAsync(request.Audit, ct);

            await transaction.CommitAsync(ct);
            return new UpdateOutcome(UpdateStatus.Updated, stamp);
        });
    }
}
