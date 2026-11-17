using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IRetentionStore"/> (finding R-10). Plain, parameterized <c>DELETE ... LIMIT</c> statements against
/// this service's own tables only. Ages are measured by the database's clock (<c>NOW()</c> for the columns it stamps itself,
/// <c>UTC_TIMESTAMP()</c> for the UTC times the service writes), never the host's, so several instances agree.
/// </summary>
public sealed class EfRetentionStore(ClinicalCareDbContext db) : IRetentionStore
{
    public async Task<PurgeResult> PurgeBatchAsync(RetentionCutoffs cutoffs, int batchSize, CancellationToken ct)
    {
        var processedDays = cutoffs.ProcessedEventDays;
        var publishedDays = cutoffs.PublishedOutboxDays;
        var unpublishedDays = cutoffs.UnpublishedOutboxDays;

        // processed_at is stamped by the database (DEFAULT CURRENT_TIMESTAMP), so it is compared with NOW().
        var processed = await db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM clinical_care_processed_events WHERE processed_at < DATE_SUB(NOW(), INTERVAL {processedDays} DAY) LIMIT {batchSize}", ct);

        // A delivered row ages from when it was delivered.
        var published = await db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM clinical_care_outbox WHERE published_at IS NOT NULL AND published_at < DATE_SUB(UTC_TIMESTAMP(), INTERVAL {publishedDays} DAY) LIMIT {batchSize}", ct);

        // A row never delivered ages from when the event happened (occurred_at is written as UTC).
        var unpublished = await db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM clinical_care_outbox WHERE published_at IS NULL AND occurred_at < DATE_SUB(UTC_TIMESTAMP(), INTERVAL {unpublishedDays} DAY) LIMIT {batchSize}", ct);

        return new PurgeResult(processed, published, unpublished);
    }
}
