using Microsoft.EntityFrameworkCore;
using OpenEmrNext.PatientManagement.Application.Ports;

namespace OpenEmrNext.PatientManagement.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IAuditLog"/>: one parameterized <c>INSERT</c> into <c>patient_management_audit</c>. It uses the context's current transaction when there is one, which is how a write's audit row
/// commits or rolls back with the write. The service's database user holds <c>INSERT</c> and <c>SELECT</c> on the table and nothing else, so the trail cannot be edited or erased through this service.
/// </summary>
public sealed class EfAuditLog(PatientManagementDbContext db) : IAuditLog
{
    public async Task AppendAsync(AuditEntry entry, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO patient_management_audit
                (at, actor, actor_id, actor_verified, action, subject, outcome, correlation_id, source_address, details)
            VALUES
                ({entry.AtUtc}, {entry.Actor.Subject}, {entry.Actor.UserId}, {entry.Actor.Verified}, {entry.Action},
                 {entry.Subject}, {entry.OutcomeName}, {entry.CorrelationId}, {entry.SourceAddress}, {entry.Details})
            """, ct);
    }
}
