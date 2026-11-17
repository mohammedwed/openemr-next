using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.Problems;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IProblemRegistry"/> and the anti-corruption layer for a problem write (LEG-2, ADR 0070): one transaction that writes
/// what legacy's REST path and its issue screen write between them, plus the outbox and audit rows, so the problem exists completely or not at all.
/// </summary>
/// <remarks>
/// Writes, in order, all parameterized (LEG-5):
/// <list type="number">
///   <item><c>lists</c>: the problem row, with <c>type = 'medical_problem'</c>, <c>activity = 1</c> and <c>date = NOW()</c> (the REST path; the issue screen's
///     <c>H:m:s</c> puts the month in the minutes, ADR 0070 #5), and the acting provider's username and group as <c>user</c> and <c>groupname</c>.</item>
///   <item><c>uuid_registry</c>: the row <c>UuidRegistry::createUuid</c> inserts for every uuid it hands out, so legacy recognises the uuid as its own.</item>
///   <item><c>lists_touch</c>: <c>INSERT IGNORE</c> of <c>(pid, 'medical_problem')</c>, as the issue screen's <c>setListTouch</c> does; legacy's summary reads it as
///     "this list has been addressed" (<c>listTouched</c>). The primary key makes it race-free.</item>
///   <item><c>clinical_care_outbox</c>: the <c>ProblemRecorded</c> event; <c>clinical_care_audit</c>: the audit row, through <see cref="IAuditLog"/> on this same transaction.</item>
/// </list>
/// Retry safety (OPS-9): if a commit succeeded but its acknowledgement was lost, the re-run looks the problem up by its uuid and stops, so nothing is
/// written twice (and so nothing is audited twice).
/// </remarks>
public sealed class EfProblemRegistry(ClinicalCareDbContext db, IAuditLog auditLog) : IProblemRegistry
{
    /// <summary>The <c>lists.type</c> this aggregate owns. Every statement in this service that touches <c>lists</c> for a problem carries it (ADR 0070 #2).</summary>
    public const string ProblemType = "medical_problem";

    public async Task RegisterAsync(Problem problem, ProblemRecordedEvent @event, AuditEntry audit, CancellationToken ct)
    {
        var uuidBytes = Rfc4122GuidConverter.ToRfc4122Bytes(problem.Uuid);
        var envelope = EventEnvelope.Serialize(
            ProblemRecordedEvent.EventType, @event.RecordedAtUtc, problem.Uuid.ToString(),
            actor: $"user:{problem.ProviderId}", @event);

        var strategy = db.Database.CreateExecutionStrategy();
        var id = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            // Already committed by an earlier attempt whose acknowledgement was lost: nothing more to write.
            var existing = await db.Database
                .SqlQuery<long>($"SELECT id AS Value FROM lists WHERE uuid = {uuidBytes} AND type = {ProblemType}")
                .ToListAsync(ct);
            if (existing.Count > 0)
                return existing[0];

            // Who did it, as legacy records it: a username and a group. Looked up from the provider id the caller named (F-CC-1).
            var actor = await LegacyActor.ResolveAsync(db, problem.ProviderId, ct);

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO lists
                    (uuid, `date`, type, title, begdate, enddate, diagnosis, outcome, occurrence, referredby, comments,
                     activity, pid, `user`, groupname)
                VALUES
                    ({uuidBytes}, {problem.RecordedAt}, {ProblemType}, {problem.Title}, {problem.BeginDate}, {problem.EndDate},
                     {problem.Diagnosis}, {problem.Outcome}, {problem.Occurrence}, {problem.ReferredBy}, {problem.Comments},
                     1, {problem.Patient.Pid}, {actor.Username}, {actor.GroupName})
                """, ct);
            var rowId = await db.Database.GetLastInsertIdAsync(ct);

            await IssueRows.RegisterUuidAsync(db, uuidBytes, ct);
            await IssueRows.TouchAsync(db, problem.Patient.Pid, ProblemType, ct);

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO clinical_care_outbox (event_type, payload, occurred_at)
                VALUES ({ProblemRecordedEvent.EventType}, {envelope}, {@event.RecordedAtUtc})
                """, ct);

            // Last statement, same transaction: the problem cannot exist without its audit row (SEC-9, ADR 0069 #3).
            await auditLog.AppendAsync(audit, ct);

            await transaction.CommitAsync(ct);
            return rowId;
        });

        problem.AssignGeneratedId(id);
    }
}
