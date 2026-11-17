using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IAllergyUpdater"/> (LEG-2, ADR 0068, ADR 0070): one transaction holding a conditional <c>UPDATE</c> of the columns an allergy owns, the
/// <c>AllergyUpdated</c> outbox row and the audit row. The same shape and guarantees as <see cref="EfProblemUpdater"/>, scoped to <c>type = 'allergy'</c>.
/// <c>reaction</c> and <c>verification</c> are NOT NULL columns, so an absent value is the empty string; <c>severity_al</c> is nullable.
/// </summary>
public sealed class EfAllergyUpdater(ClinicalCareDbContext db, IAuditLog auditLog) : IAllergyUpdater
{
    public async Task<UpdateOutcome> UpdateAsync(AllergyUpdateRequest request, CancellationToken ct)
    {
        var uuidBytes = Rfc4122GuidConverter.ToRfc4122Bytes(request.AllergyUuid);
        var change = request.Change;
        var expected = request.Expected.Stamp;
        var reaction = change.Reaction ?? "";
        var verification = change.Verification ?? "";
        var envelope = EventEnvelope.Serialize(
            AllergyUpdatedEvent.EventType, request.Event.UpdatedAtUtc, request.AllergyUuid.ToString(), actor: request.Audit.Actor.Subject, request.Event);

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var matched = await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE lists
                SET title = {change.Title}, begdate = {change.BeginDate}, enddate = {change.EndDate}, diagnosis = {change.Diagnosis},
                    reaction = {reaction}, severity_al = {change.Severity}, verification = {verification}, comments = {change.Comments}
                WHERE uuid = {uuidBytes} AND type = {EfAllergyRegistry.AllergyType} AND modifydate = {expected}
                """, ct);

            if (matched == 0)
            {
                var current = await db.Database
                    .SqlQuery<DateTime>(
                        $"SELECT modifydate AS Value FROM lists WHERE uuid = {uuidBytes} AND type = {EfAllergyRegistry.AllergyType}")
                    .ToListAsync(ct);

                return current.Count == 0
                    ? new UpdateOutcome(UpdateStatus.NotFound, null)
                    : new UpdateOutcome(UpdateStatus.Conflict, current[0]);
            }

            var stamp = (await db.Database
                .SqlQuery<DateTime>($"SELECT modifydate AS Value FROM lists WHERE uuid = {uuidBytes} AND type = {EfAllergyRegistry.AllergyType}")
                .ToListAsync(ct)).Single();

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO clinical_care_outbox (event_type, payload, occurred_at)
                VALUES ({AllergyUpdatedEvent.EventType}, {envelope}, {request.Event.UpdatedAtUtc})
                """, ct);

            await auditLog.AppendAsync(request.Audit, ct);

            await transaction.CommitAsync(ct);
            return new UpdateOutcome(UpdateStatus.Updated, stamp);
        });
    }
}
