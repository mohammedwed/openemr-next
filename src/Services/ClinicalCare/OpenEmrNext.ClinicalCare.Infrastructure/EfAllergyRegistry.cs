using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.Allergies;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IAllergyRegistry"/> and the anti-corruption layer for an allergy write (LEG-2, ADR 0070): one transaction that writes what
/// legacy's REST path and its issue screen write between them, plus the outbox and audit rows, so the allergy exists completely or not at all.
/// </summary>
/// <remarks>
/// Writes, in order, all parameterized (LEG-5): the <c>lists</c> row (<c>type = 'allergy'</c>, <c>activity = 1</c>, <c>date = NOW()</c> as the REST path does;
/// <c>reaction</c> and <c>verification</c> are NOT NULL columns, so an absent one is written as the empty string legacy's column default holds), then the
/// shared <see cref="IssueRows"/> (the <c>uuid_registry</c> row and the <c>lists_touch</c> marker for the allergy list), the <c>AllergyRecorded</c> outbox
/// row, and the audit row through <see cref="IAuditLog"/> on this same transaction. A retry after a lost commit acknowledgement looks the allergy up by its
/// uuid and stops, so nothing is written or audited twice (OPS-9).
/// </remarks>
public sealed class EfAllergyRegistry(ClinicalCareDbContext db, IAuditLog auditLog) : IAllergyRegistry
{
    /// <summary>The <c>lists.type</c> this aggregate owns. Every statement in this service that touches <c>lists</c> for an allergy carries it (ADR 0070 #2).</summary>
    public const string AllergyType = "allergy";

    public async Task RegisterAsync(Allergy allergy, AllergyRecordedEvent @event, AuditEntry audit, CancellationToken ct)
    {
        var uuidBytes = Rfc4122GuidConverter.ToRfc4122Bytes(allergy.Uuid);
        var envelope = EventEnvelope.Serialize(
            AllergyRecordedEvent.EventType, @event.RecordedAtUtc, allergy.Uuid.ToString(),
            actor: audit.Actor.Subject, @event);

        var strategy = db.Database.CreateExecutionStrategy();
        var id = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var existing = await db.Database
                .SqlQuery<long>($"SELECT id AS Value FROM lists WHERE uuid = {uuidBytes} AND type = {AllergyType}")
                .ToListAsync(ct);
            if (existing.Count > 0)
                return existing[0];

            var actor = await LegacyActor.ResolveAsync(db, allergy.ProviderId, ct);
            var reaction = allergy.Reaction ?? "";
            var verification = allergy.Verification ?? "";

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO lists
                    (uuid, `date`, type, title, begdate, enddate, diagnosis, reaction, severity_al, verification, comments,
                     activity, pid, `user`, groupname)
                VALUES
                    ({uuidBytes}, {allergy.RecordedAt}, {AllergyType}, {allergy.Title}, {allergy.BeginDate}, {allergy.EndDate},
                     {allergy.Diagnosis}, {reaction}, {allergy.Severity}, {verification}, {allergy.Comments},
                     1, {allergy.Patient.Pid}, {actor.Username}, {actor.GroupName})
                """, ct);
            var rowId = await db.Database.GetLastInsertIdAsync(ct);

            await IssueRows.RegisterUuidAsync(db, uuidBytes, ct);
            await IssueRows.TouchAsync(db, allergy.Patient.Pid, AllergyType, ct);

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO clinical_care_outbox (event_type, payload, occurred_at)
                VALUES ({AllergyRecordedEvent.EventType}, {envelope}, {@event.RecordedAtUtc})
                """, ct);

            // Last statement, same transaction: the allergy cannot exist without its audit row (SEC-9, ADR 0069 #3).
            await auditLog.AppendAsync(audit, ct);

            await transaction.CommitAsync(ct);
            return rowId;
        });

        allergy.AssignGeneratedId(id);
    }
}
