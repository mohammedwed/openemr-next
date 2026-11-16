using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using OpenEmrNext.PatientManagement.Application.Ports;

namespace OpenEmrNext.PatientManagement.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IOutboxRelayStore"/> for <c>patient_management_outbox</c> (EVT-7).
/// </summary>
/// <remarks>
/// <b>Claiming.</b> One short transaction: lock the oldest unpublished rows (<c>FOR UPDATE</c>, deliberately without
/// <c>SKIP LOCKED</c>, which would let a second relay jump over rows the first holds and break order), keep those that
/// are due, stop at the first that is not, and stamp a lease on the ones kept. Delivery then happens **outside** any
/// transaction, so a slow consumer never holds database locks. A relay that dies leaves leases that simply expire.
/// All times are UTC from the database's own clock, so relays on different hosts agree.
/// <para>
/// The raw ADO command is used for the locking read because EF wraps a <c>SqlQuery</c> in a derived table, in which
/// <c>FOR UPDATE</c> is not valid.
/// </para>
/// </remarks>
public sealed class EfOutboxRelayStore(PatientManagementDbContext db) : IOutboxRelayStore
{
    public async Task<IReadOnlyList<OutboxEntry>> ClaimAsync(int batchSize, TimeSpan lease, CancellationToken ct)
    {
        var leaseSeconds = (int)Math.Ceiling(lease.TotalSeconds);
        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var connection = db.Database.GetDbConnection();
            var claimed = new List<OutboxEntry>();

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction.GetDbTransaction();
                command.CommandText =
                    """
                    SELECT id, event_type, payload, occurred_at, attempts,
                           (COALESCE(locked_until, '1970-01-01') <= UTC_TIMESTAMP()
                            AND COALESCE(next_attempt_at, '1970-01-01') <= UTC_TIMESTAMP()) AS due
                    FROM patient_management_outbox
                    WHERE published_at IS NULL AND dead_lettered_at IS NULL
                    ORDER BY id
                    LIMIT @batch
                    FOR UPDATE
                    """;
                var batch = command.CreateParameter();
                batch.ParameterName = "@batch";
                batch.Value = batchSize;
                command.Parameters.Add(batch);

                await using var reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    // Order is the contract: the first row that is not due (in back-off, or held by another relay)
                    // ends the claim, so nothing behind it is delivered ahead of it.
                    if (reader.GetInt64(5) == 0)
                        break;

                    claimed.Add(new OutboxEntry(
                        reader.GetInt64(0), reader.GetString(1), reader.GetString(2),
                        DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Utc), reader.GetInt32(4)));
                }
            }

            foreach (var entry in claimed)
            {
                var id = entry.Id;
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE patient_management_outbox SET locked_until = DATE_ADD(UTC_TIMESTAMP(), INTERVAL {leaseSeconds} SECOND) WHERE id = {id}", ct);
            }

            await transaction.CommitAsync(ct);
            return (IReadOnlyList<OutboxEntry>)claimed;
        });
    }

    public async Task MarkPublishedAsync(long id, CancellationToken ct) =>
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE patient_management_outbox SET published_at = UTC_TIMESTAMP(), locked_until = NULL, last_error = NULL WHERE id = {id}", ct);

    public async Task MarkFailedAsync(long id, string error, TimeSpan retryAfter, bool deadLetter, CancellationToken ct)
    {
        var seconds = (int)Math.Ceiling(retryAfter.TotalSeconds);
        var dead = deadLetter ? 1 : 0;
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE patient_management_outbox
            SET attempts = attempts + 1, last_error = {error}, locked_until = NULL,
                next_attempt_at = DATE_ADD(UTC_TIMESTAMP(), INTERVAL {seconds} SECOND),
                dead_lettered_at = CASE WHEN {dead} = 1 THEN UTC_TIMESTAMP() ELSE NULL END
            WHERE id = {id}
            """, ct);
    }

    public async Task ReleaseAsync(IReadOnlyCollection<long> ids, CancellationToken ct)
    {
        foreach (var id in ids)
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE patient_management_outbox SET locked_until = NULL WHERE id = {id}", ct);
    }
}
