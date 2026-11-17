using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IEncounterUpdater"/> and the anti-corruption layer for an encounter edit (LEG-2, ADR 0068): one transaction
/// holding a conditional <c>UPDATE</c> of the columns this service owns, the <c>EncounterUpdated</c> outbox row, and the audit row.
/// </summary>
/// <remarks>
/// <para>
/// The statement is conditional on the row's stamp (<c>last_update = expected</c>) and names only the editable columns, so a billing write or
/// a legacy edit of any other column is neither overwritten nor disturbed (LEG-10). It also refuses a row whose <c>newpatient</c> form legacy has
/// soft-deleted (CC-9). The service user holds <c>UPDATE</c> on exactly those columns and nothing else (<c>LeastPrivilegeGrantTests</c>).
/// </para>
/// <para>
/// "Zero rows matched" means the stamp moved or the row is gone, never "the values were identical": MySqlConnector's default
/// (<c>UseAffectedRows=false</c>) counts matched rows, not changed ones. A second read tells the two apart.
/// </para>
/// <para>
/// Retry safety (OPS-9): a lost commit acknowledgement makes the context re-run the delegate. The re-run finds the stamp already advanced by its
/// own first attempt and reports a conflict, never a second write: the event and audit row were committed once, and the caller reloads and sees
/// its change. Unlike a create there is no natural key to recognise the repeat by, so this is the accepted behaviour, not an oversight.
/// </para>
/// </remarks>
public sealed class EfEncounterUpdater(ClinicalCareDbContext db, IAuditLog auditLog) : IEncounterUpdater
{
    public async Task<UpdateOutcome> UpdateAsync(EncounterUpdateRequest request, CancellationToken ct)
    {
        var uuidBytes = Rfc4122GuidConverter.ToRfc4122Bytes(request.EncounterUuid);
        var change = request.Change;
        var expected = request.Expected.Stamp;
        var envelope = EventEnvelope.Serialize(
            EncounterUpdatedEvent.EventType, request.Event.UpdatedAtUtc, request.EncounterUuid.ToString(),
            actor: request.Audit.Actor.Subject, request.Event);

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var matched = await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE form_encounter fe
                SET fe.date = {change.Date}, fe.onset_date = {change.OnsetDate}, fe.reason = {change.Reason},
                    fe.class_code = {change.ClassCode}, fe.pc_catid = {change.CalendarCategoryId},
                    fe.facility_id = {change.FacilityId}, fe.facility = {change.FacilityName},
                    fe.pos_code = {change.PosCode}, fe.provider_id = {change.ProviderId}
                WHERE fe.uuid = {uuidBytes}
                  AND fe.last_update = {expected}
                  AND NOT EXISTS (SELECT 1 FROM forms f WHERE f.encounter = fe.encounter AND f.pid = fe.pid
                                  AND f.formdir = 'newpatient' AND f.deleted <> 0)
                """, ct);

            if (matched == 0)
            {
                // Nothing was changed, so there is nothing to commit. Say whether the row moved or vanished.
                var current = await db.Database
                    .SqlQuery<DateTime>(
                        $"""
                        SELECT fe.last_update AS Value FROM form_encounter fe
                        WHERE fe.uuid = {uuidBytes}
                          AND NOT EXISTS (SELECT 1 FROM forms f WHERE f.encounter = fe.encounter AND f.pid = fe.pid
                                          AND f.formdir = 'newpatient' AND f.deleted <> 0)
                        """)
                    .ToListAsync(ct);

                return current.Count == 0
                    ? new UpdateOutcome(UpdateStatus.NotFound, null)
                    : new UpdateOutcome(UpdateStatus.Conflict, current[0]);
            }

            var stamp = (await db.Database
                .SqlQuery<DateTime>($"SELECT last_update AS Value FROM form_encounter WHERE uuid = {uuidBytes}")
                .ToListAsync(ct)).Single();

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO clinical_care_outbox (event_type, payload, occurred_at)
                VALUES ({EncounterUpdatedEvent.EventType}, {envelope}, {request.Event.UpdatedAtUtc})
                """, ct);

            // Last statement, same transaction: the change cannot commit without its audit row (SEC-9, ADR 0069 #3).
            await auditLog.AppendAsync(request.Audit, ct);

            await transaction.CommitAsync(ct);
            return new UpdateOutcome(UpdateStatus.Updated, stamp);
        });
    }
}
