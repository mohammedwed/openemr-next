using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Application.Ports;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IIdempotencyStore"/> (ADR 0077): this service's own <c>clinical_care_idempotency</c> table, primary key (user, key). Atomicity comes from the primary key, not from a read-then-write:
/// a request inserts its claim with a fresh token, then reads the row back, and owns the key only if the token it reads is its own.
/// </summary>
/// <remarks>
/// Rows older than the window are removed as a side effect of claiming (the table needs no job). A claim still in progress after the lease is taken over, on the reasoning that a request which has not finished in
/// that long has died; the only duplicate this can allow is a request that died in the instant between committing its record and saving its answer. Only the hash of the request is stored, plus the answer
/// (identifiers, the status, the location).
/// </remarks>
public sealed class EfIdempotencyStore(ClinicalCareDbContext db) : IIdempotencyStore
{
    public async Task<ClaimResult> ClaimAsync(long actorId, string key, string fingerprint, DateTime nowUtc, CancellationToken ct)
    {
        var windowStart = nowUtc - Idempotency.Window;
        var leaseStart = nowUtc - Idempotency.Lease;
        var token = Guid.NewGuid();

        // A day-old row is gone. This key's own is removed first so it can be reused; a bounded sweep of others keeps the table small without a job.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM clinical_care_idempotency WHERE actor_id = {actorId} AND idem_key = {key} AND claimed_at < {windowStart}", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM clinical_care_idempotency WHERE claimed_at < {windowStart} LIMIT 100", ct);

        // INSERT IGNORE here is safe (R-15 was about numeric clamping): every value is validated to fit its column before it gets here.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT IGNORE INTO clinical_care_idempotency (actor_id, idem_key, fingerprint, state, claim_token, claimed_at)
            VALUES ({actorId}, {key}, {fingerprint}, 'started', {token.ToString()}, {nowUtc})
            """, ct);

        var row = await ReadAsync(actorId, key, ct);
        if (row is null)
            return await ClaimAsync(actorId, key, fingerprint, nowUtc, ct);   // removed between the insert and the read: try again

        if (row.ClaimToken == token.ToString())
            return new ClaimResult(ClaimOutcome.Claimed, token);

        if (!string.Equals(row.Fingerprint, fingerprint, StringComparison.Ordinal))
            return new ClaimResult(ClaimOutcome.Mismatch);

        if (row.State == "done")
            return new ClaimResult(ClaimOutcome.Replay, Stored: new StoredResponse(row.StatusCode ?? 200, row.ContentType, row.Location, row.Body));

        if (row.ClaimedAt >= leaseStart)
            return new ClaimResult(ClaimOutcome.InProgress);

        // The earlier request has held the key past the lease without finishing: take it over, but only if it is still that same stale claim.
        var taken = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE clinical_care_idempotency SET claim_token = {token.ToString()}, claimed_at = {nowUtc}
            WHERE actor_id = {actorId} AND idem_key = {key} AND state = 'started' AND claim_token = {row.ClaimToken} AND claimed_at < {leaseStart}
            """, ct);
        return taken == 1 ? new ClaimResult(ClaimOutcome.Claimed, token) : new ClaimResult(ClaimOutcome.InProgress);
    }

    public async Task CompleteAsync(long actorId, string key, Guid token, StoredResponse response, DateTime nowUtc, CancellationToken ct) =>
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE clinical_care_idempotency
            SET state = 'done', status_code = {response.StatusCode}, content_type = {response.ContentType}, location = {response.Location}, body = {response.Body}, completed_at = {nowUtc}
            WHERE actor_id = {actorId} AND idem_key = {key} AND claim_token = {token.ToString()}
            """, ct);

    public async Task ReleaseAsync(long actorId, string key, Guid token, CancellationToken ct) =>
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM clinical_care_idempotency WHERE actor_id = {actorId} AND idem_key = {key} AND claim_token = {token.ToString()} AND state = 'started'", ct);

    private async Task<Row?> ReadAsync(long actorId, string key, CancellationToken ct) =>
        (await db.Database
            .SqlQuery<Row>(
                $"""
                SELECT fingerprint AS Fingerprint, state AS State, claim_token AS ClaimToken, claimed_at AS ClaimedAt, status_code AS StatusCode,
                       content_type AS ContentType, location AS Location, body AS Body
                FROM clinical_care_idempotency WHERE actor_id = {actorId} AND idem_key = {key}
                """)
            .ToListAsync(ct)).FirstOrDefault();

    private sealed class Row
    {
        public string Fingerprint { get; set; } = "";
        public string State { get; set; } = "";
        public string ClaimToken { get; set; } = "";
        public DateTime ClaimedAt { get; set; }
        public int? StatusCode { get; set; }
        public string? ContentType { get; set; }
        public string? Location { get; set; }
        public string? Body { get; set; }
    }
}
