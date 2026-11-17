using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IAuditLog"/>: one parameterized INSERT into <c>clinical_care_audit</c> (SEC-9, ADR 0069).
/// </summary>
/// <remarks>
/// It shares the registry's <see cref="ClinicalCareDbContext"/>, so when a registry has a transaction open (it always does for a
/// change) the insert runs inside it and commits or rolls back with the business write. Nothing here opens a transaction of its own,
/// which is what keeps "the change and its audit row stand or fall together" true. The service's database user holds only INSERT
/// and SELECT on the table, so even a bug here could not edit or remove a row.
/// </remarks>
public sealed class EfAuditLog(ClinicalCareDbContext db) : IAuditLog
{
    public async Task AppendAsync(AuditEntry entry, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO clinical_care_audit
                (at, actor, actor_id, actor_verified, action, subject, outcome, correlation_id, source_address, details)
            VALUES
                ({entry.AtUtc}, {entry.Actor.Subject}, {entry.Actor.UserId}, {entry.Actor.Verified}, {entry.Action},
                 {entry.SubjectText}, {entry.OutcomeName}, {entry.CorrelationId}, {entry.SourceAddress}, {entry.Details})
            """, ct);
    }
}
